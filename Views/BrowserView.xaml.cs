using System.Text;
using System.Web;
using System.Windows.Input;
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
    private static readonly string[] ValidInternalPaths = { "default", "preview", "about" };
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly ICacheService _cache;
    private readonly IOpalClient _geminiClient;
    private readonly IIdentityService _identityService;
    private readonly List<Task> _parallelRenderWorkload;
    private readonly Stack<Uri> _recentHistory;
    private readonly ISettingsDatabase _settingsDatabase;
    private readonly ILogger<BrowserView> _logger;

    private bool _canPrint;
    private bool _canShowHostCertificate;
    private string _findNextQuery;
    private string _htmlTemplate;
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
    private bool _resetFindNext;

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

#if ANDROID
        WebViewHandler.Mapper.AppendToMapping("CreateAndroidPrintService",
            (handler, _) => _printService = new AndroidPrintService(handler.PlatformView));
        WebViewHandler.Mapper.AppendToMapping("SetClearFindResultsHandler",
            (handler, _) => ClearMatches += (_, _) => handler.PlatformView.ClearMatches());
        WebViewHandler.Mapper.AppendToMapping("SetFindInPageHandler",
            (handler, _) => FindNext += (_, _) =>
            {
                if (_resetFindNext) // new query
                    handler.PlatformView.FindAllAsync(FindNextQuery);
                else // existing query; continue forward
                    handler.PlatformView.FindNext(true);
            });
        WebViewHandler.Mapper.AppendToMapping("SetFindListener",
            (handler, _) => handler.PlatformView.SetFindListener(new CallbackFindListener(count =>
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
            })));
        RefreshViewHandler.Mapper.AppendToMapping("SetRefreshIndicatorOffset",
            (handler, _) => handler.PlatformView.SetProgressViewOffset(false, 0, (int)Window.Height / 4));
#endif
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
        // first pop the current page, then peek to get the prior
        if (_recentHistory.TryPop(out var current))
        {
            if (_recentHistory.TryPeek(out var prev))
            {
                Location = prev;
                return true;
            }

            // there was no previous entry; re-push the current one in order to revert the stack to its initial state
            _recentHistory.Push(current);
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
        var bucket = uri.Host.ToUpperInvariant();
        var key = uri.ToString().ToUpperInvariant();

        if (_cache.ResourceExists(bucket, key))
        {
            var image = new MemoryStream();
            await _cache.LoadResource(bucket, key, image);
            return CreateInlineImageDataUrl(image);
        }

        return null;
    }

    private async Task<string> FetchAndCacheInlinedImage(Uri uri)
    {
        var bucket = uri.Host.ToUpperInvariant();
        var key = uri.ToString().ToUpperInvariant();

        for (var i = 0; i < 6; i++)
        {
            try
            {
                if (await _geminiClient.SendRequestAsync(uri.ToString()) is SuccessfulResponse success)
                {
                    var image = await CreateInlinedImagePreview(success.Body, success.MimeType);
                    image.Seek(0, SeekOrigin.Begin);
                    await _cache.StoreResource(bucket, key, image);
                    return CreateInlineImageDataUrl(image);
                }

                // if the error was only temporary (according to the server), then
                // we can try again
            }
            catch (Exception)
            {
                // don't care
            }

            await Task.Delay(Convert.ToInt32(Math.Pow(2, i) * 100));
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
        var fileName = line.Uri.Segments.LastOrDefault()?.Trim('/');

        if (!string.IsNullOrWhiteSpace(fileName) && MimeTypes.TryGetMimeType(fileName, out var mimeType) &&
            mimeType.StartsWith("image"))
        {
            var node = HtmlNode.CreateNode("<p></p>");

            if (line.Uri.Scheme == "gemini")
            {
                var cached = await TryLoadCachedImage(line.Uri);
                if (cached != null)
                    node.AppendChild(RenderInlineImageFigure(line, cached));
                else
                {
                    _parallelRenderWorkload.Add(Task.Run(async () =>
                    {
                        var source = await FetchAndCacheInlinedImage(line.Uri);
                        if (!string.IsNullOrEmpty(source))
                            node.AppendChild(RenderInlineImageFigure(line, source));
                        else
                        {
                            // did not load the image preview; fallback to a simple link
                            node.AppendChild(HtmlNode.CreateNode(
                                $"<a href=\"{line.Uri}\">{HttpUtility.HtmlEncode(line.Text ?? line.Uri.ToString())}</a>"));
                        }
                    }));
                }
            }
            else
            {
                // http, etc. can be handled by the browser
                node.AppendChild(RenderInlineImageFigure(line, line.Uri.ToString()));
            }

            return node;
        }

        return RenderDefaultLinkLine(line);
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
            EmptyLine => null,
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

    private string RenderCachedHtml(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
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
        await _cache.StoreString(gemtext.Uri, _input, document.DocumentNode.OuterHtml);

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

    public async Task LoadPage(bool triggeredByRefresh = false)
    {
        if (_isLoading || string.IsNullOrEmpty(_htmlTemplate))
            return;

        CanShowHostCertificate = false;

        if (Location == null || Location.Scheme == Constants.InternalScheme)
        {
            await LoadInternalPage(Location?.Host ?? "default");
            IsRefreshing = false;
            _isLoading = false;
            return;
        }

        var finished = false;
        var remainingAttempts = 5;
        var attempt = 0;

        _isLoading = true;

        if (!IsRefreshing)
            IsRefreshing = true;

        CanPrint = false;

        if (HasFindNextQuery)
            ClearFindResults();

        do
        {
            if (!triggeredByRefresh)
            {
                var cached = await _cache.LoadString(Location, Input);
                if (!string.IsNullOrEmpty(cached))
                {
                    RenderedHtml = RenderCachedHtml(cached);
                    CanShowHostCertificate = true;
                    RenderUrl = $"{Location.Host}{Location.PathAndQuery}";
                    StoreVisitedLocation(Location, false);
                    CanPrint = _printService != null;
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(Input))
                Location = new UriBuilder(Location) { Query = Input }.Uri;

            var response = await _geminiClient.SendRequestAsync(Location.ToString());

            RenderUrl = $"{response.Uri.Host}{response.Uri.PathAndQuery}";

            switch (response)
            {
                case InputRequiredResponse inputRequired:
                {
                    Input = await _parentPage.DisplayPromptAsync(Text.BrowserView_LoadPage_Input_Required,
                        inputRequired.Message);
                    if (string.IsNullOrEmpty(Input))
                    {
                        _settingsDatabase.LastVisitedUrl = response.Uri.ToString();
                        finished = true; // if no user-input was provided, then we cannot continue
                    }

                    break;
                }
                case ErrorResponse error:
                {
                    if (!error.CanRetry)
                    {
                        // Opal has indicated that this request should not be re-sent; bail early
                        //
                        // Currently this only happens in the case of invalid or rejected remote
                        // certificates, where re-sending the request would not make sense
                        finished = true;
                        break;
                    }

                    if (remainingAttempts == 1 || !IsRetryAppropriate(error.Status))
                    {
                        await _parentPage.DisplayAlert(Text.BrowserView_LoadPage_Error, error.Message,
                            Text.BrowserView_LoadPage_OK);
                        finished = true;
                    }
                    else
                        await Task.Delay(Convert.ToInt32(Math.Pow(2, attempt) * 100));

                    remainingAttempts--;
                    break;
                }
                case SuccessfulResponse success:
                {
                    Location = response.Uri;

                    if (success is GemtextResponse gemtext)
                    {
                        CanShowHostCertificate = true;
                        RenderedHtml = await RenderGemtextAsHtml(gemtext);
                        StoreVisitedLocation(Location, false);
                        CanPrint = _printService != null;
                    }
                    else
                    {
                        StoreVisitedLocation(Location, true);

                        // not gemtext; save as a file
                        var fileName = response.Uri.Segments.LastOrDefault() ??
                                       GetDefaultFileNameByMimeType(success.MimeType);
                        var path = Path.Combine(FileSystem.CacheDirectory, fileName);
                        await using (var outputFile = File.Create(path))
                        {
                            await success.Body.CopyToAsync(outputFile);
                        }

                        await Launcher.Default.OpenAsync(
                            new OpenFileRequest(fileName, new ReadOnlyFile(path, success.MimeType)));
                    }

                    finished = true;
                    break;
                }
            }

            attempt++;
        } while (!finished);

        IsRefreshing = false;
        _isLoading = false;

        Input = null;
    }

    private void StoreVisitedLocation(Uri uri, bool isExternal)
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

    private async Task LoadInternalPage(string name = "default")
    {
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

    private async void PageWebView_OnNavigating(object sender, WebNavigatingEventArgs e)
    {
        var uri = e.Url.ToUri();
        if (!uri.IsAbsoluteUri || uri.Scheme == "gemini")
            Location = uri;
        else
            await Launcher.Default.OpenAsync(uri);

        e.Cancel = true;
    }

    private async Task LoadPageTemplates()
    {
        if (!string.IsNullOrEmpty(_htmlTemplate))
            return;

        await using var template = await FileSystem.OpenAppPackageFileAsync("template.html");
        using var reader = new StreamReader(template);
        _htmlTemplate = await reader.ReadToEndAsync();
    }

    private async void BrowserView_OnLoaded(object sender, EventArgs e)
    {
        await LoadPageTemplates();

        _parentPage = this.FindParentPage();

        if (Location == null)
            await LoadInternalPage();
        else
            await LoadPage();
    }

    protected virtual void OnFindNext()
    {
        FindNext?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnClearFindNext()
    {
        ClearMatches?.Invoke(this, EventArgs.Empty);
    }
}