using System.Text;
using System.Web;
using System.Windows.Input;
using HtmlAgilityPack;
using Microsoft.Maui.Handlers;
using Opal;
using Opal.Authentication.Certificate;
using Opal.Document.Line;
using Opal.Response;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Platforms.Android;
using RosyCrow.Services.Cache;
using RosyCrow.Services.Identity;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class BrowserView : ContentView
{
    private static readonly string[] ValidInternalPaths = { "default", "preview", "about" };
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly IOpalClient _geminiClient;
    private readonly IIdentityService _identityService;
    private readonly Stack<Uri> _recentHistory;
    private readonly ISettingsDatabase _settingsDatabase;
    private readonly ICacheService _cache;

    private bool _canPrint;
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

    public BrowserView()
        : this(MauiProgram.Services.GetRequiredService<IOpalClient>(),
            MauiProgram.Services.GetRequiredService<ISettingsDatabase>(),
            MauiProgram.Services.GetRequiredService<IBrowsingDatabase>(),
            MauiProgram.Services.GetRequiredService<IIdentityService>(),
            MauiProgram.Services.GetRequiredService<ICacheService>())
    {
    }

    public BrowserView(IOpalClient geminiClient, ISettingsDatabase settingsDatabase, IBrowsingDatabase browsingDatabase,
        IIdentityService identityService, ICacheService cache)
    {
        InitializeComponent();

        BindingContext = this;

        _geminiClient = geminiClient;
        _settingsDatabase = settingsDatabase;
        _browsingDatabase = browsingDatabase;
        _identityService = identityService;
        _cache = cache;
        _recentHistory = new Stack<Uri>();

        Refresh = new Command(async () => await LoadPage(true));

        _geminiClient.GetActiveCertificateCallback = GetActiveCertificateCallback;

#if ANDROID
        WebViewHandler.Mapper.AppendToMapping("CreateAndroidPrintService",
            (handler, _) => _printService = new AndroidPrintService(handler.PlatformView));
        RefreshViewHandler.Mapper.AppendToMapping("SetRefreshIndicatorOffset",
            (handler, _) => handler.PlatformView.SetProgressViewOffset(false, 0, (int)Window.Height / 4));
#endif
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

    private async Task<IClientCertificate> GetActiveCertificateCallback()
    {
        if (_identityService.ShouldReloadActiveCertificate)
            return new ClientCertificate(await _identityService.LoadActiveCertificate());

        if (_identityService.ActiveCertificate != null)
            return new ClientCertificate(_identityService.ActiveCertificate);

        return null;
    }

    public void Print()
    {
        _printService.Print(PageTitle);
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

    private static string GetDefaultFileNameByMimeType(string mimeType)
    {
        var extension = MimeTypes.GetMimeTypeExtensions(mimeType).FirstOrDefault();
        return extension != null
            ? $"file_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.{extension}"
            : $"file_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }

    private static HtmlNode RenderGemtextLine(ILine line)
    {
        return line switch
        {
            EmptyLine => null,
            HeadingLine headingLine => HtmlNode.CreateNode(
                $"<h{headingLine.Level}>{HttpUtility.HtmlEncode(headingLine.Text)}</h{headingLine.Level}>"),
            LinkLine linkLine => HtmlNode.CreateNode(
                $"<p><a href=\"{linkLine.Uri}\">{HttpUtility.HtmlEncode(linkLine.Text ?? linkLine.Uri.ToString())}</a></p>"),
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
                case FormattedBeginLine:
                    preNode = HtmlNode.CreateNode("<pre></pre>");
                    preText.Clear();
                    break;
                case FormattedEndLine when preNode != null:
                    preNode.InnerHtml = HttpUtility.HtmlEncode(preText.ToString());
                    body.AppendChild(preNode);
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

                    var renderedLine = RenderGemtextLine(line);
                    if (renderedLine != null)
                        body.AppendChild(renderedLine);

                    break;
            }

        if (listNode != null)
            body.AppendChild(listNode);

        // cache the page prior to injecting the stylesheet
        await _cache.WriteCached(gemtext.Uri, _input, document.DocumentNode.OuterHtml);

        InjectStylesheet(document.DocumentNode);

        return document.DocumentNode.OuterHtml;
    }

    private static bool IsRetryAppropriate(StatusCode status)
    {
        // only retry requests that could potentially return a different result

        switch (status)
        {
            case StatusCode.Unknown:
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

        if (Location == null || Location.Scheme == Constants.InternalScheme)
        {
            await LoadInternalPage(Location?.Host);
            IsRefreshing = false;
            _isLoading = false;
            return;
        }

        var finished = false;
        var remainingAttempts = 5;

        _isLoading = true;

        if (!IsRefreshing)
            IsRefreshing = true;

        CanPrint = false;

        do
        {
            if (!triggeredByRefresh)
            {
                var cached = await _cache.TryGetCached(Location, Input);
                if (!string.IsNullOrEmpty(cached))
                {
                    RenderedHtml = RenderCachedHtml(cached);
                    RenderUrl = $"{Location.Host}{Location.PathAndQuery}";
                    StoreVisitedLocation(Location);
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
                    Input = await _parentPage.DisplayPromptAsync("Input Required", inputRequired.Message);
                    if (string.IsNullOrEmpty(Input))
                    {
                        _settingsDatabase.LastVisitedUrl = response.Uri.ToString();
                        finished = true; // if no user-input was provided, then we cannot continue
                    }

                    break;
                }
                case ErrorResponse error:
                {
                    if (remainingAttempts == 1 || !IsRetryAppropriate(error.Status))
                    {
                        await _parentPage.DisplayAlert("Error", error.Message, "OK");
                        finished = true;
                    }
                    else
                        await Task.Delay(1000 / remainingAttempts);

                    remainingAttempts--;
                    break;
                }
                case SuccessfulResponse success:
                {
                    Location = response.Uri;

                    if (success is GemtextResponse gemtext)
                    {
                        RenderedHtml = await RenderGemtextAsHtml(gemtext);
                        CanPrint = _printService != null;
                    }
                    else
                    {
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

                    StoreVisitedLocation(Location);

                    finished = true;
                    break;
                }
            }
        } while (!finished);

        IsRefreshing = false;
        _isLoading = false;

        Input = null;
    }

    private void StoreVisitedLocation(Uri uri)
    {
        _settingsDatabase.LastVisitedUrl = uri.ToString();

        if (!_recentHistory.TryPeek(out var prev) || !prev.Equals(uri))
            _recentHistory.Push(uri);

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
}