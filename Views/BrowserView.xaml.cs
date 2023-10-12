using System.Text;
using System.Web;
using System.Windows.Input;
using Android.Views;
using Android.Webkit;
using CommunityToolkit.Maui.Core;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Graphics.Platform;
using Microsoft.Maui.Handlers;
using Opal;
using Opal.Authentication.Certificate;
using Opal.CallbackArgs;
using Opal.Document.Line;
using Opal.Response;
using Opal.Tofu;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Platforms.Android;
using RosyCrow.Resources.Localization;
using RosyCrow.Services.Cache;
using RosyCrow.Services.Identity;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class BrowserView : ContentView
{
    private const int MaxRequestAttempts = 5;
    private static readonly string[] ValidInternalPaths = { "default", "preview", "about" };
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly ICacheService _cache;
    private readonly IOpalClient _geminiClient;
    private readonly IIdentityService _identityService;
    private readonly ILogger<BrowserView> _logger;
    private readonly List<Task> _parallelRenderWorkload;
    private readonly Stack<Uri> _recentHistory;
    private readonly ISettingsDatabase _settingsDatabase;

    private bool _canPrint;
    private bool _canShowHostCertificate;
    private string _findNextQuery;
    private string _input;
    private bool _isLoading;
    private bool _isPageLoaded;
    private bool _isRefreshing;
    private Uri _location;
    private string _pageTitle;
    private ContentPage _parentPage;
    private IPrintService _printService;
    private ICommand _refresh;
    private string _renderedHtml;
    private string _renderUrl;
    private string _htmlTemplate;
    private bool _resetFindNext;

    private enum ResponseAction
    {
        Retry,
        Finished
    }

    public BrowserView()
        : this(MauiProgram.Services.GetRequiredService<IOpalClient>(),
            MauiProgram.Services.GetRequiredService<ISettingsDatabase>(),
            MauiProgram.Services.GetRequiredService<IBrowsingDatabase>(),
            MauiProgram.Services.GetRequiredService<IIdentityService>(),
            MauiProgram.Services.GetRequiredService<ICacheService>(),
            MauiProgram.Services.GetRequiredService<ILogger<BrowserView>>())
    {
    }

    public BrowserView(IOpalClient geminiClient, ISettingsDatabase settingsDatabase, IBrowsingDatabase browsingDatabase,
        IIdentityService identityService, ICacheService cache, ILogger<BrowserView> logger)
    {
        InitializeComponent();

        BindingContext = this;

        _geminiClient = geminiClient;
        _settingsDatabase = settingsDatabase;
        _browsingDatabase = browsingDatabase;
        _identityService = identityService;
        _cache = cache;
        _logger = logger;
        _recentHistory = new Stack<Uri>();
        _parallelRenderWorkload = new List<Task>();

        Refresh = new Command(async () => await LoadPage(true));

        _geminiClient.GetActiveClientCertificateCallback = GetActiveCertificateCallback;
        _geminiClient.RemoteCertificateInvalidCallback = RemoteCertificateInvalidCallback;
        _geminiClient.RemoteCertificateUnrecognizedCallback = RemoteCertificateUnrecognizedCallback;
    }

    public bool CanShowHostCertificate
    {
        get => _canShowHostCertificate;
        set
        {
            if (value == _canShowHostCertificate) return;
            _canShowHostCertificate = value;
            OnPropertyChanged();
        }
    }

    public ICommand Refresh
    {
        get => _refresh;
        set
        {
            if (Equals(value, _refresh)) return;
            _refresh = value;
            OnPropertyChanged();
        }
    }

    public string RenderedHtml
    {
        get => _renderedHtml;
        set
        {
            if (value == _renderedHtml) return;
            _renderedHtml = value;
            OnPropertyChanged();
        }
    }

    public string PageTitle
    {
        get => _pageTitle;
        set
        {
            if (value == _pageTitle) return;
            _pageTitle = value;
            OnPropertyChanged();
        }
    }

    public bool CanPrint
    {
        get => _canPrint;
        set
        {
            if (value == _canPrint) return;
            _canPrint = value;
            OnPropertyChanged();
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            if (value == _isRefreshing) return;
            _isRefreshing = value;
            OnPropertyChanged();
        }
    }

    public Uri Location
    {
        get => _location;
        set
        {
            if (value != _location)
            {
                _location = value;
                OnPropertyChanged();
            }

            if (!_isLoading)
                Dispatcher.Dispatch(async () => await LoadPage());

            OnPropertyChanged(nameof(IsPageLoaded));
        }
    }

    public string Input
    {
        get => _input;
        set
        {
            if (value == _input) return;
            _input = value;
            OnPropertyChanged();
        }
    }

    public string RenderUrl
    {
        get => _renderUrl;
        set
        {
            if (value == _renderUrl) return;
            _renderUrl = value;
            OnPropertyChanged();
        }
    }

    public bool IsPageLoaded
    {
        get => _isPageLoaded;
        set
        {
            if (value == _isPageLoaded) return;
            _isPageLoaded = value;
            OnPropertyChanged();
        }
    }

    public bool HasFindNextQuery => !string.IsNullOrEmpty(FindNextQuery);

    public string FindNextQuery
    {
        get => _findNextQuery;
        private set
        {
            if (value == _findNextQuery)
                return;
            _findNextQuery = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasFindNextQuery));
        }
    }

    private async Task RemoteCertificateUnrecognizedCallback(RemoteCertificateUnrecognizedArgs arg)
    {
        await Dispatcher.DispatchAsync(async () =>
            arg.AcceptAndTrust = await _parentPage.DisplayAlert(
                Text.BrowserView_RemoteCertificateUnrecognizedCallback_New_Certificate,
                string.Format(
                    Text
                        .BrowserView_RemoteCertificateUnrecognizedCallback_Accept_the_host_s_new_certificate_and_continue___Its_fingerprint_is__0__,
                    arg.Fingerprint), Text.BrowserView_RemoteCertificateUnrecognizedCallback_Yes,
                Text.BrowserView_RemoteCertificateUnrecognizedCallback_No));

        if (arg.AcceptAndTrust)
            _browsingDatabase.AcceptHostCertificate(arg.Host);
    }

    private async Task RemoteCertificateInvalidCallback(RemoteCertificateInvalidArgs arg)
    {
        var message = arg.Reason switch
        {
            InvalidCertificateReason.NameMismatch => Text
                .BrowserView_RemoteCertificateInvalidCallback_The_name_on_the_server_s_certificate_is_incorrect_,
            InvalidCertificateReason.TrustedMismatch => Text
                .BrowserView_RemoteCertificateInvalidCallback_The_host_s_certificate_has_changed_,
            InvalidCertificateReason.Expired => Text
                .BrowserView_RemoteCertificateInvalidCallback_The_host_s_certificate_has_expired_,
            InvalidCertificateReason.NotYet => Text
                .BrowserView_RemoteCertificateInvalidCallback_The_host_s_certificate_is_not_valid_yet_,
            InvalidCertificateReason.Other => Text
                .BrowserView_RemoteCertificateInvalidCallback_The_host_s_certificate_is_invalid_,
            InvalidCertificateReason.MissingInformation => Text
                .BrowserView_RemoteCertificateInvalidCallback_The_host_s_certificate_is_missing_required_information_,
            _ => throw new ArgumentOutOfRangeException()
        };

        await Dispatcher.DispatchAsync(() =>
            _parentPage.DisplayAlert(Text.BrowserView_RemoteCertificateInvalidCallback_Certificate_Problem, message,
                Text.BrowserView_RemoteCertificateInvalidCallback_Cancel));
    }

    private async Task<IClientCertificate> GetActiveCertificateCallback()
    {
        if (_identityService.ShouldReloadActiveCertificate)
            return new ClientCertificate(await _identityService.LoadActiveCertificate());

        if (_identityService.ActiveCertificate != null)
            return new ClientCertificate(_identityService.ActiveCertificate);

        return null;
    }

    private event EventHandler FindNext;

    private event EventHandler ClearMatches;

    public void Print()
    {
        try
        {
            _printService.Print(PageTitle);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while attempting to print the current page");
        }
    }

    public bool GoBack()
    {
        try
        {
            // first pop the current page, then peek to get the prior
            if (_recentHistory.TryPop(out var current))
            {
                if (_recentHistory.TryPeek(out var prev))
                {
                    // navigate to the prior page but do 
                    _location = prev;
                    Dispatcher.Dispatch(async () => await LoadPage(useCache: true));
                    return true;
                }

                // there was no previous entry; re-push the current one in order to revert the stack to its initial state
                _recentHistory.Push(current);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while navigating backward");
        }

        return false;
    }

    public void SimulateLocationChanged()
    {
        OnPropertyChanged(nameof(Location));
    }

    public void ClearFindResults()
    {
        FindNextQuery = null;
        OnClearFindNext();
    }

    public void FindTextInPage(string query)
    {
        // if the query is different from the last time, then start the search over from the top of the page
        if (query != FindNextQuery)
            _resetFindNext = true;

        FindNextQuery = query;

        OnFindNext();
        _resetFindNext = false;
    }

    private static string GetDefaultFileNameByMimeType(string mimeType)
    {
        var extension = MimeTypes.GetMimeTypeExtensions(mimeType).FirstOrDefault();
        return extension != null
            ? $"file_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.{extension}"
            : $"file_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }

    private static async Task<MemoryStream> CreateInlinedImagePreview(Stream source, string mimetype)
    {
        var typeHint = mimetype switch
        {
            "image/jpeg" => ImageFormat.Jpeg,
            "image/gif" => ImageFormat.Gif,
            "image/bmp" => ImageFormat.Bmp,
            "image/tiff" => ImageFormat.Tiff,
            _ => ImageFormat.Png
        };

        var image = PlatformImage.FromStream(source, typeHint);
        using var downsized = image.Downsize(256.0f, true);

        var output = new MemoryStream();
        await downsized.SaveAsync(output);

        return output;
    }

    private string CreateInlineImageDataUrl(MemoryStream data)
    {
        data.Seek(0, SeekOrigin.Begin);
        return $"data:image/png;base64,{Convert.ToBase64String(data.ToArray())}";
    }

    private async Task<string> TryLoadCachedImage(Uri uri)
    {
        try
        {
            var image = new MemoryStream();
            if (await _cache.TryRead(uri, image))
            {
                _logger.LogDebug("Loaded cached image originally from {URI}", uri);
                return CreateInlineImageDataUrl(image);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while attempting to load a cached image retrieved from {URI}", uri);
        }

        return null;
    }

    private async Task<string> FetchAndCacheInlinedImage(Uri uri)
    {
        try
        {
            for (var i = 0; i < MaxRequestAttempts; i++)
            {
                try
                {
                    _geminiClient.AllowIPv6 = _settingsDatabase.AllowIpv6;

                    if (await _geminiClient.SendRequestAsync(uri.ToString()) is SuccessfulResponse success)
                    {
                        _logger.LogDebug("Successfully loaded an image of type {MimeType} to be inlined from {URI}",
                            success.MimeType, uri);

                        var image = await CreateInlinedImagePreview(success.Body, success.MimeType);

                        if (image == null)
                        {
                            _logger.LogWarning(
                                "Loaded an image to be inlined from {URI} but failed to create the preview", uri);
                            break;
                        }

                        image.Seek(0, SeekOrigin.Begin);
                        await _cache.Write(uri, image);

                        _logger.LogDebug("Loaded an inlined image from {URI} after {Attempt} attempt(s)", uri, i + 1);

                        return CreateInlineImageDataUrl(image);
                    }

                    // if the error was only temporary (according to the server), then
                    // we can try again
                }
                catch (Exception e)
                {
                    // don't care
                    _logger.LogDebug(e, "Attempt {Attempt} to fetch and inline an image from {URI} failed", i + 1, uri);
                }

                await Task.Delay(Convert.ToInt32(Math.Pow(2, i) * 100));
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while attempting to cache an image from {URI}", uri);
        }

        return null;
    }

    private async Task<HtmlNode> RenderLinkLine(LinkLine line)
    {
        return _settingsDatabase.InlineImages
            ? await RenderInlineImage(line)
            : RenderDefaultLinkLine(line);
    }

    private static HtmlNode RenderDefaultLinkLine(LinkLine line)
    {
        return HtmlNode.CreateNode(
            $"<p><a href=\"{line.Uri}\">{HttpUtility.HtmlEncode(line.Text ?? line.Uri.ToString())}</a></p>");
    }

    private async Task<HtmlNode> RenderInlineImage(LinkLine line)
    {
        try
        {
            var fileName = line.Uri.Segments.LastOrDefault()?.Trim('/');

            string mimeType = null;
            if (!string.IsNullOrWhiteSpace(fileName) && MimeTypes.TryGetMimeType(fileName, out mimeType) &&
                mimeType.StartsWith("image"))
            {
                var node = HtmlNode.CreateNode("<p></p>");

                _logger.LogDebug("Attempting to render an image preview inline from {URI}", line.Uri);

                if (line.Uri.Scheme == Constants.GeminiScheme)
                {
                    _logger.LogDebug("The image URI specifies the gemini protocol");

                    var cached = await TryLoadCachedImage(line.Uri);
                    if (cached != null)
                    {
                        _logger.LogDebug("Loading the image preview from the cache");
                        node.AppendChild(RenderInlineImageFigure(line, cached));
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Queueing the image download to complete after the rest of the page has been rendered.");
                        _parallelRenderWorkload.Add(Task.Run(async () =>
                        {
                            var source = await FetchAndCacheInlinedImage(line.Uri);
                            if (!string.IsNullOrEmpty(source))
                            {
                                _logger.LogDebug("Successfully created the image preview; rendering that now");
                                node.AppendChild(RenderInlineImageFigure(line, source));
                            }
                            else
                            {
                                // did not load the image preview; fallback to a simple link
                                _logger.LogDebug(
                                    "Could not create the image preview; falling-back to a simple gemtext link line");
                                node.AppendChild(HtmlNode.CreateNode(
                                    $"<a href=\"{line.Uri}\">{HttpUtility.HtmlEncode(line.Text ?? line.Uri.ToString())}</a>"));
                            }
                        }));
                    }
                }
                else
                {
                    // http, etc. can be handled by the browser
                    _logger.LogDebug(
                        "The image URI specifies the HTTP protocol; let the WebView figure out how to render it");
                    node.AppendChild(RenderInlineImageFigure(line, line.Uri.ToString()));
                }

                return node;
            }

            _logger.LogDebug(
                "The URI {URI} does not appear to point to an image (type: {MimeType}); an anchor tag will be rendered",
                line.Uri, mimeType ?? "none");
            return RenderDefaultLinkLine(line);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while rendering an inline image preview from {URI}", line.Uri);
            return null;
        }
    }

    private static HtmlNode RenderInlineImageFigure(LinkLine line, string source)
    {
        // successfully loaded the image preview
        var figure = HtmlNode.CreateNode($"<figure><img src=\"{source}\" /></figure>");

        if (!string.IsNullOrWhiteSpace(line.Text))
        {
            figure.AppendChild(
                HtmlNode.CreateNode($"<figcaption>{HttpUtility.HtmlEncode(line.Text)}</figcaption>"));
        }

        var anchor = HtmlNode.CreateNode($"<a href=\"{line.Uri}\"></a>");
        anchor.AppendChild(figure);

        return anchor;
    }

    private async Task<HtmlNode> RenderGemtextLine(ILine line)
    {
        return line switch
        {
            EmptyLine => HtmlNode.CreateNode("<br>"),
            HeadingLine headingLine => HtmlNode.CreateNode(
                $"<h{headingLine.Level}>{HttpUtility.HtmlEncode(headingLine.Text)}</h{headingLine.Level}>"),
            LinkLine linkLine => await RenderLinkLine(linkLine),
            QuoteLine quoteLine => HtmlNode.CreateNode(
                $"<blockquote><p>{HttpUtility.HtmlEncode(quoteLine.Text)}</p></blockquote>"),
            TextLine textLine => HtmlNode.CreateNode($"<p>{HttpUtility.HtmlEncode(textLine.Text)}</p>"),
            _ => throw new ArgumentOutOfRangeException(nameof(line))
        };
    }

    private void InjectStylesheet(HtmlNode documentNode)
    {
        documentNode.ChildNodes.FindFirst("head")
            .AppendChild(HtmlNode.CreateNode(
                $"<link rel=\"stylesheet\" href=\"Themes/{_settingsDatabase.Theme}.css\" media=\"screen\" />"));
    }

    private string RenderCachedHtml(Stream buffer)
    {
        var document = new HtmlDocument();
        document.Load(buffer);
        var childNodes = document.DocumentNode.ChildNodes;
        PageTitle = (childNodes.FindFirst("h1") ?? childNodes.FindFirst("h2") ?? childNodes.FindFirst("h3"))?.InnerText;
        InjectStylesheet(document.DocumentNode);
        return document.DocumentNode.OuterHtml;
    }

    private async Task<string> RenderGemtextAsHtml(GemtextResponse gemtext)
    {
        var document = new HtmlDocument();
        document.DocumentNode.AppendChild(HtmlNode.CreateNode(_htmlTemplate));

        var body = document.DocumentNode.ChildNodes.FindFirst("main");

        PageTitle = null; // this will be set to the first heading we encounter

        HtmlNode preNode = null;
        HtmlNode listNode = null;
        var preText = new StringBuilder();
        foreach (var line in gemtext.AsDocument())
            switch (line)
            {
                case FormattedBeginLine preBegin:
                    var preParent = body.AppendChild(HtmlNode.CreateNode("<figure></figure>"));
                    if (!string.IsNullOrWhiteSpace(preBegin.Text))
                        preParent.AppendChild(HtmlNode.CreateNode($"<figcaption>{preBegin.Text}</figcaption>"));
                    preNode = preParent.AppendChild(HtmlNode.CreateNode("<pre></pre>"));
                    preText.Clear();
                    break;
                case FormattedEndLine when preNode != null:
                    preNode.InnerHtml = HttpUtility.HtmlEncode(preText.ToString());
                    break;
                case FormattedLine formatted:
                    preText.AppendLine(formatted.Text);
                    break;
                case ListLine listLine:
                    listNode ??= HtmlNode.CreateNode("<ul></ul>");
                    listNode.AppendChild(HtmlNode.CreateNode($"<li>{HttpUtility.HtmlEncode(listLine.Text)}</li>"));
                    break;
                default:
                    if (listNode != null)
                    {
                        body.AppendChild(listNode);
                        listNode = null;
                    }

                    if (PageTitle == null && line is HeadingLine heading)
                        PageTitle = heading.Text;

                    var renderedLine = await RenderGemtextLine(line);
                    if (renderedLine != null)
                        body.AppendChild(renderedLine);

                    break;
            }

        if (listNode != null)
            body.AppendChild(listNode);

        if (_parallelRenderWorkload.Any())
        {
            await Task.WhenAll(_parallelRenderWorkload.ToArray());
            _parallelRenderWorkload.Clear();
        }

        // cache the page prior to injecting the stylesheet
        await using var pageBuffer = new MemoryStream(Encoding.UTF8.GetBytes(document.DocumentNode.OuterHtml));
        await _cache.Write(gemtext.Uri, pageBuffer);

        InjectStylesheet(document.DocumentNode);

        return document.DocumentNode.OuterHtml;
    }

    private static bool IsRetryAppropriate(StatusCode status)
    {
        // only retry requests that could potentially return a different result

        switch (status)
        {
            case StatusCode.TemporaryFailure:
            case StatusCode.ServerUnavailable:
            case StatusCode.CgiError:
            case StatusCode.ProxyError:
            case StatusCode.SlowDown:
                return true;
            default:
                return false;
        }
    }

    public async Task Upload(TitanPayload payload)
    {
        if (_isLoading)
            return;

        _isLoading = true;

        if (!IsRefreshing)
            IsRefreshing = true;

        try
        {
            for (var attempts = 0; attempts < MaxRequestAttempts; attempts++)
            {
                if (!string.IsNullOrWhiteSpace(Input))
                {
                    _logger.LogInformation("User provided input \"{Input}\"", Input);
                    Location = new UriBuilder(Location) { Query = Input }.Uri;
                }

                _geminiClient.AllowIPv6 = _settingsDatabase.AllowIpv6;

                var response = await _geminiClient.UploadAsync(Location, payload.Size, payload.Token, payload.MimeType, payload.Contents);

                if (await HandleTitanResponse(response, attempts) == ResponseAction.Finished)
                {
                    _logger.LogInformation("Upload finished after {Attempts} attempt(s)", attempts + 1);
                    break;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while navigating to {URI}", Location);
        }

        IsRefreshing = false;
        _isLoading = false;
    }

    public async Task LoadPage(bool triggeredByRefresh = false, bool useCache = false)
    {
        if (_isLoading || string.IsNullOrEmpty(_htmlTemplate))
            return;

        CanShowHostCertificate = false;

        if (Location == null || Location.Scheme == Constants.InternalScheme)
        {
            await LoadInternalPage(Location?.Host ?? "default");
            if (Location != null)
                RenderUrl = $"{Location.Host}{Location.PathAndQuery}";
            IsRefreshing = false;
            _isLoading = false;
            return;
        }

        if (Location.Scheme == Constants.TitanScheme)
        {
            await Navigation.PushModalPageAsync<TitanUploadPage>(page => page.Browser = this);
            return;
        }

        _isLoading = true;

        if (!IsRefreshing)
            IsRefreshing = true;

        CanPrint = false;

        if (HasFindNextQuery)
            ClearFindResults();

        _logger.LogInformation("Navigating to {URI}", Location);

        try
        {
            for (var attempts = 0; attempts < MaxRequestAttempts; attempts++)
            {
                if (useCache && !triggeredByRefresh)
                {
                    var cached = new MemoryStream();
                    if (await _cache.TryRead(Location, cached))
                    {
                        _logger.LogInformation("Loading a cached copy of the page");

                        cached.Seek(0, SeekOrigin.Begin);
                        RenderedHtml = RenderCachedHtml(cached);
                        CanShowHostCertificate = true;
                        RenderUrl = $"{Location.Host}{Location.PathAndQuery}";
                        StoreVisitedLocation(Location, false);
                        CanPrint = _printService != null;
                        break;
                    }
                }

                if (!string.IsNullOrWhiteSpace(Input))
                {
                    _logger.LogInformation("User provided input \"{Input}\"", Input);
                    Location = new UriBuilder(Location) { Query = Input }.Uri;
                }

                _geminiClient.AllowIPv6 = _settingsDatabase.AllowIpv6;

                var response = await _geminiClient.SendRequestAsync(Location);

                RenderUrl = $"{response.Uri.Host}{response.Uri.PathAndQuery}";
                _logger.LogInformation("Response was {Response}", response);

                if (await HandleGeminiResponse(response, attempts) == ResponseAction.Finished)
                {
                    _logger.LogInformation("Request finished after {Attempts} attempt(s)", attempts + 1);
                    break;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while navigating to {URI}", Location);
        }

        IsRefreshing = false;
        _isLoading = false;

        Input = null;
    }

    private async Task<ResponseAction> HandleTitanResponse(IGeminiResponse response, int attempt)
    {
        switch (response)
        {
            case InputRequiredResponse inputRequired:
            {
                Input = await _parentPage.DisplayPromptAsync(Text.BrowserView_LoadPage_Input_Required,
                    inputRequired.Message);

                if (string.IsNullOrEmpty(Input))
                    return ResponseAction.Finished; // if no user-input was provided, then we cannot continue

                return ResponseAction.Retry;
            }
            case ErrorResponse error:
            {
                if (!error.CanRetry)
                    return ResponseAction.Finished;

                _logger.LogInformation("{Attempts} attempt(s) remaining", MaxRequestAttempts - attempt);

                if (MaxRequestAttempts - attempt <= 1 || !IsRetryAppropriate(error.Status))
                {
                    _logger.LogInformation("No further attempts will be made");
                    await _parentPage.DisplayAlert(Text.BrowserView_LoadPage_Error, error.Message, Text.BrowserView_LoadPage_OK);
                    return ResponseAction.Finished;
                }

                await Task.Delay(Convert.ToInt32(Math.Pow(2, attempt) * 100));

                return ResponseAction.Retry;
            }
            case SuccessfulResponse success:
            {
                await HandleSuccessfulResponse(success);
                return ResponseAction.Finished;
            }
            default:
                return ResponseAction.Retry;
        }
    }

    private async Task<ResponseAction> HandleGeminiResponse(IGeminiResponse response, int attempt)
    {
        switch (response)
        {
            case InputRequiredResponse inputRequired:
            {
                Input = await _parentPage.DisplayPromptAsync(Text.BrowserView_LoadPage_Input_Required,
                    inputRequired.Message);

                if (string.IsNullOrEmpty(Input))
                {
                    _settingsDatabase.LastVisitedUrl = response.Uri.ToString();
                    return ResponseAction.Finished; // if no user-input was provided, then we cannot continue
                }

                return ResponseAction.Retry;
            }
            case ErrorResponse error:
            {
                if (!error.CanRetry)
                {
                    // Opal has indicated that this request should not be re-sent; bail early
                    //
                    // Currently this only happens in the case of invalid or rejected remote
                    // certificates, where re-sending the request would not make sense
                    return ResponseAction.Finished;
                }

                _logger.LogInformation("{Attempts} attempt(s) remaining", MaxRequestAttempts - attempt);

                if (MaxRequestAttempts - attempt <= 1 || !IsRetryAppropriate(error.Status))
                {
                    _logger.LogInformation("No further attempts will be made");
                    await _parentPage.DisplayAlert(Text.BrowserView_LoadPage_Error,
                        error.Message,
                        Text.BrowserView_LoadPage_OK);
                    return ResponseAction.Finished;
                }

                await Task.Delay(Convert.ToInt32(Math.Pow(2, attempt) * 100));

                return ResponseAction.Retry;
            }
            case SuccessfulResponse success:
            {
                await HandleSuccessfulResponse(success);
                return ResponseAction.Finished;
            }
            default:
                return ResponseAction.Retry;
        }
    }

    private async Task HandleSuccessfulResponse(SuccessfulResponse response)
    {
        Location = response.Uri;

        if (response is GemtextResponse gemtext)
        {
            _logger.LogInformation("Response is a gemtext document");

            CanShowHostCertificate = true;
            RenderedHtml = await RenderGemtextAsHtml(gemtext);
            StoreVisitedLocation(Location, false);
            CanPrint = _printService != null;
        }
        else
        {
            _logger.LogInformation(
                "Response is not a gemtext document, so it will be opened externally");

            StoreVisitedLocation(Location, true);

            // not gemtext; save as a file
            var fileName = response.Uri.Segments.LastOrDefault() ??
                           GetDefaultFileNameByMimeType(response.MimeType);
            var path = Path.Combine(FileSystem.CacheDirectory, fileName);

            await using (var outputFile = File.Create(path))
                await response.Body.CopyToAsync(outputFile);

            _logger.LogInformation("Opening file {Path}", path);

            await Launcher.Default.OpenAsync(
                new OpenFileRequest(fileName, new ReadOnlyFile(path, response.MimeType)));
        }
    }

    private void StoreVisitedLocation(Uri uri, bool isExternal)
    {
        try
        {
            if (!isExternal)
            {
                _settingsDatabase.LastVisitedUrl = uri.ToString();

                if (!_recentHistory.TryPeek(out var prev) || !prev.Equals(uri))
                    _recentHistory.Push(uri);
            }

            if (_settingsDatabase.SaveVisited)
            {
                _browsingDatabase.AddVisitedPage(new Visited
                {
                    Url = uri.ToString(), Timestamp = DateTime.Now,
                    Title = _pageTitle ?? uri.Segments.LastOrDefault() ?? uri.Host
                });
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while storing a visited page {URI}", uri);
        }
    }

    private async Task LoadInternalPage(string name = "default")
    {
        try
        {
            _logger.LogInformation("Loading internal page {Name}", name);

            var document = new HtmlDocument();
            document.DocumentNode.AppendChild(HtmlNode.CreateNode(_htmlTemplate));

            InjectStylesheet(document.DocumentNode);

            var body = document.DocumentNode.ChildNodes.FindFirst("main");

            await using (var file = await FileSystem.OpenAppPackageFileAsync($"{name}.html"))
            using (var reader = new StreamReader(file))
            {
                body.AppendChild(HtmlNode.CreateNode(await reader.ReadToEndAsync()));
            }

            RenderUrl = $"{Constants.InternalScheme}://{name}";

            RenderedHtml = document.DocumentNode.OuterHtml;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while loading internal page {Name}", name);
        }
    }

    private async void PageWebView_OnNavigating(object sender, WebNavigatingEventArgs e)
    {
        var uri = e.Url.ToUri();
        if (!uri.IsAbsoluteUri || uri.Scheme is Constants.GeminiScheme or Constants.TitanScheme or Constants.InternalScheme)
            Location = uri;
        else
            await Launcher.Default.OpenAsync(uri);

        e.Cancel = true;
    }

    private static async Task<string> LoadPageTemplate()
    {
        await using var template = await FileSystem.OpenAppPackageFileAsync("template.html");
        using var reader = new StreamReader(template);
        return await reader.ReadToEndAsync();
    }

    public async Task Setup(Element parent)
    {
        _htmlTemplate = await LoadPageTemplate();
        _parentPage = this.FindParentPage(parent);
        //
        // if (Location == null)
        //     await LoadInternalPage();
        // else
        //     await LoadPage();
    }

    protected virtual void OnFindNext()
    {
        FindNext?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnClearFindNext()
    {
        ClearMatches?.Invoke(this, EventArgs.Empty);
    }


#if ANDROID
    private static void BuildContextMenu(IMenu menu, Android.Webkit.WebView view)
    {
        var hitTest = view.GetHitTestResult();

        if (hitTest.Type is HitTestResult.AnchorType or HitTestResult.SrcAnchorType or HitTestResult.SrcImageAnchorType &&
            !string.IsNullOrWhiteSpace(hitTest.Extra))
        {
            menu.Add(Text.BrowserView_BuildContextMenu_Copy_URL)?.SetOnMenuItemClickListener(
                new ActionMenuClickHandler<string>(hitTest.Extra,
                    async uri => await Clipboard.Default.SetTextAsync(uri)));
            menu.Add(Text.BrowserView_BuildContextMenu_Share_URL)?.SetOnMenuItemClickListener(
                new ActionMenuClickHandler<string>(hitTest.Extra,
                    async uri => await Share.Default.RequestAsync(new ShareTextRequest(uri))));
        }
    }
#endif

    private void PageWebView_OnHandlerChanged(object sender, EventArgs e)
    {
#if ANDROID
        var webViewHandler = (sender as Microsoft.Maui.Controls.WebView)?.Handler as WebViewHandler;

        webViewHandler.PlatformView.ContextMenuCreated +=
            (o, args) => BuildContextMenu(args.Menu, o as Android.Webkit.WebView);

        _printService = new AndroidPrintService(webViewHandler.PlatformView);

        ClearMatches += (_, _) => webViewHandler.PlatformView.ClearMatches();

        FindNext += (_, _) =>
        {
            if (_resetFindNext) // new query
                webViewHandler.PlatformView.FindAllAsync(FindNextQuery);
            else // existing query; continue forward
                webViewHandler.PlatformView.FindNext(true);
        };

        webViewHandler.PlatformView.SetFindListener(new CallbackFindListener(count =>
        {
            if (!_resetFindNext)
                return;

            if (count == 0)
            {
                _parentPage.ShowToast(Text.BrowserView_FindNext_No_instances_found, ToastDuration.Short);
                FindNextQuery = null;
            }
            else
            {
                _parentPage.ShowToast(string.Format(Text.BrowserView_FindNext_Found__0__instances, count),
                    ToastDuration.Short);
            }
        }));
#endif
    }

    private void RefreshView_OnHandlerChanged(object sender, EventArgs e)
    {
#if ANDROID
        var refreshViewHandler = (sender as RefreshView)?.Handler as RefreshViewHandler;

        // having to access the window this way is weird and bad, hopefully GetVisualElementWindow() will work one day
        if (Application.Current != null && Application.Current.Windows.FirstOrDefault() is { } window)
            refreshViewHandler?.PlatformView.SetProgressViewOffset(false, 0, (int)window.Height / 4);
#endif
    }
}