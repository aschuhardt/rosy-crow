using System.Text;
using System.Web;
using System.Windows.Input;
using CommunityToolkit.Maui.Core;
using HtmlAgilityPack;
using Microsoft.Maui.Handlers;
using Opal;
using Opal.Document.Line;
using Opal.Response;
using Yarrow.Extensions;
using Yarrow.Interfaces;
using Yarrow.Models;
#if ANDROID
using Yarrow.Platforms.Android;
#endif

// ReSharper disable AsyncVoidLambda

namespace Yarrow;

public partial class MainPage : ContentPage
{
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly IOpalClient _geminiClient;
    private readonly Stack<Uri> _recentHistory;
    private readonly ISettingsDatabase _settingsDatabase;

    private bool _canPrint;
    private string _entryUrl;
    private ICommand _expandMenu;
    private ICommand _hideMenu;
    private string _htmlTemplate;
    private string _input;
    private bool _isMenuExpanded;
    private bool _isNavBarVisible;
    private bool _isRefreshing;
    private ICommand _loadEnteredUrl;
    private ICommand _loadHomeUrl;
    private Uri _location;
    private Animation _menuHideAnimation;
    private Animation _menuShowAnimation;
    private ICommand _openBookmarks;
    private ICommand _openHistory;
    private ICommand _openIdentity;
    private ICommand _openSettings;
    private string _pageTitle;
    private ICommand _print;
    private IPrintService _printService;
    private ICommand _refresh;
    private string _renderedHtml;
    private ICommand _setHomeUrl;
    private ICommand _toggleBookmarked;
    private ICommand _toggleMenuExpanded;

    public MainPage(ISettingsDatabase settingsDatabase, IBrowsingDatabase browsingDatabase, IOpalClient geminiClient)
    {
        InitializeComponent();

        _settingsDatabase = settingsDatabase;
        _browsingDatabase = browsingDatabase;
        _geminiClient = geminiClient;
        _recentHistory = new Stack<Uri>();
        _isNavBarVisible = true;

        BindingContext = this;
        Refresh = new Command(async _ => await LoadPage());
        LoadEnteredUrl = new Command<string>(url => Location = url.ToGeminiUri());
        ToggleMenuExpanded = new Command(() => IsMenuExpanded = !IsMenuExpanded);
        HideMenu = new Command(() => IsMenuExpanded = false);
        ExpandMenu = new Command(() => IsMenuExpanded = true);
        Print = new Command(() => _printService?.Print(_pageTitle));
        LoadHomeUrl = new Command(TryLoadHomeUrl);
        SetHomeUrl = new Command(TrySetHomeUrl);
        ToggleBookmarked = new Command(TryToggleBookmarked);
        OpenBookmarks = new Command(async () => await Navigation.PushPageAsync<BookmarksPage>());
        OpenHistory = new Command(async () => await Navigation.PushPageAsync<HistoryPage>());
        OpenIdentity = new Command(async () => await Navigation.PushPageAsync<IdentityPage>());
        OpenSettings = new Command(async () => await Navigation.PushPageAsync<SettingsPage>());

        WebViewHandler.Mapper.AppendToMapping("WebViewScrollingAware", (handler, _) =>
        {
#if ANDROID
            handler.PlatformView.ScrollChange += (sender, args) =>
            {
                if (args.ScrollY > args.OldScrollY + 5)
                    IsNavBarVisible = false;
                else if (args.ScrollY < args.OldScrollY - 20 || args.ScrollY == 0)
                    IsNavBarVisible = true;
            };

            _printService = new AndroidPrintService(handler.PlatformView);
#endif
        });
    }

    public ICommand OpenSettings
    {
        get => _openSettings;
        set
        {
            if (Equals(value, _openSettings)) return;
            _openSettings = value;
            OnPropertyChanged();
        }
    }

    public ICommand OpenBookmarks
    {
        get => _openBookmarks;
        set
        {
            if (Equals(value, _openBookmarks)) return;
            _openBookmarks = value;
            OnPropertyChanged();
        }
    }

    public ICommand OpenIdentity
    {
        get => _openIdentity;
        set
        {
            if (Equals(value, _openIdentity)) return;
            _openIdentity = value;
            OnPropertyChanged();
        }
    }

    public ICommand OpenHistory
    {
        get => _openHistory;
        set
        {
            if (Equals(value, _openHistory)) return;
            _openHistory = value;
            OnPropertyChanged();
        }
    }

    public bool IsNavBarVisible
    {
        get => _isNavBarVisible;
        set
        {
            if (value == _isNavBarVisible) return;
            _isNavBarVisible = value;
            OnPropertyChanged();
            PerformNavBarAnimations();
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

    public bool IsMenuExpanded
    {
        get => _isMenuExpanded;
        set
        {
            if (value == _isMenuExpanded) return;
            _isMenuExpanded = value;
            OnPropertyChanged();
            PerformMenuAnimations();
        }
    }

    public string EntryUrl
    {
        get => _entryUrl;
        set
        {
            if (value == _entryUrl) return;
            _entryUrl = value;
            OnPropertyChanged();
        }
    }

    public ICommand Print
    {
        get => _print;
        set
        {
            if (Equals(value, _print)) return;
            _print = value;
            OnPropertyChanged();
        }
    }

    public ICommand ToggleMenuExpanded
    {
        get => _toggleMenuExpanded;
        set
        {
            if (Equals(value, _toggleMenuExpanded)) return;
            _toggleMenuExpanded = value;
            OnPropertyChanged();
        }
    }

    public ICommand ExpandMenu
    {
        get => _expandMenu;
        set
        {
            if (Equals(value, _expandMenu)) return;
            _expandMenu = value;
            OnPropertyChanged();
        }
    }

    public ICommand HideMenu
    {
        get => _hideMenu;
        set
        {
            if (Equals(value, _hideMenu)) return;
            _hideMenu = value;
            OnPropertyChanged();
        }
    }

    public ICommand LoadEnteredUrl
    {
        get => _loadEnteredUrl;
        set
        {
            if (Equals(value, _loadEnteredUrl)) return;
            _loadEnteredUrl = value;
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
            if (value == _location) return;
            _location = value;
            OnPropertyChanged();
            Dispatcher.Dispatch(async () => await LoadPage());
            OnPropertyChanged(nameof(IsPageLoaded));
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

    public ICommand ToggleBookmarked
    {
        get => _toggleBookmarked;
        set
        {
            if (Equals(value, _toggleBookmarked)) return;
            _toggleBookmarked = value;
            OnPropertyChanged();
        }
    }

    public ICommand SetHomeUrl
    {
        get => _setHomeUrl;
        set
        {
            if (Equals(value, _setHomeUrl)) return;
            _setHomeUrl = value;
            OnPropertyChanged();
        }
    }

    public ICommand LoadHomeUrl
    {
        get => _loadHomeUrl;
        set
        {
            if (Equals(value, _loadHomeUrl)) return;
            _loadHomeUrl = value;
            OnPropertyChanged();
        }
    }

    public bool IsPageLoaded => Location != null;

    private void PerformNavBarAnimations()
    {
        if (IsNavBarVisible)
        {
            Dispatcher.Dispatch(async () =>
                await NavBar.TranslateTo(NavBar.TranslationX, 0));
        }
        else
        {
            // also collapse the menu if the navbar is being hidden
            if (IsMenuExpanded)
                IsMenuExpanded = false;

            new Animation(v => NavBar.TranslationY = v, 0, -NavBar.Height).Commit(this, "HideNavBar");
        }
    }

    private void PerformMenuAnimations()
    {
        if (IsMenuExpanded)
            _menuShowAnimation.Commit(this, "ShowMenu");
        else
            _menuHideAnimation.Commit(this, "HideAnimation");
    }

    protected override bool OnBackButtonPressed()
    {
        // if the menu is visible, just close it
        if (IsMenuExpanded)
        {
            IsMenuExpanded = false;
            return true;
        }

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

    private void TryLoadHomeUrl()
    {
        if (string.IsNullOrEmpty(_settingsDatabase.HomeUrl))
            this.ShowToast("No home URL has been set. Long-press the Home button to set one.", ToastDuration.Long);
        else
            Location = _settingsDatabase.HomeUrl.ToGeminiUri();
    }

    private void TrySetHomeUrl()
    {
        if (Location == null)
            return;

        _settingsDatabase.HomeUrl = Location.ToString();

        OnPropertyChanged(nameof(Location)); // force buttons to update

        this.ShowToast("Home set", ToastDuration.Short);
    }

    private void TryToggleBookmarked()
    {
        if (Location == null)
            return;

        if (_browsingDatabase.IsBookmark(Location, out var bookmark))
        {
            _browsingDatabase.Bookmarks.Remove(bookmark);

            OnPropertyChanged(nameof(Location)); // force buttons to update

            this.ShowToast("Bookmark removed", ToastDuration.Short);
        }
        else
        {
            _browsingDatabase.Bookmarks.Add(new Bookmark { Title = _pageTitle, Url = Location.ToString() });

            OnPropertyChanged(nameof(Location)); // force buttons to update

            this.ShowToast("Bookmark added", ToastDuration.Short);
        }
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

    private string RenderGemtextAsHtml(GemtextResponse gemtext)
    {
        var document = new HtmlDocument();
        document.DocumentNode.AppendChild(HtmlNode.CreateNode(_htmlTemplate));

        var body = document.DocumentNode.ChildNodes.FindFirst("main");

        _pageTitle = null; // this will be set to the first heading we encounter

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

                    if (_pageTitle == null && line is HeadingLine heading)
                        _pageTitle = heading.Text;

                    var renderedLine = RenderGemtextLine(line);
                    if (renderedLine != null)
                        body.AppendChild(renderedLine);

                    break;
            }

        if (listNode != null)
            body.AppendChild(listNode);

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

    private async Task LoadPage()
    {
        if (Location == null)
            return;

        var finished = false;
        var remainingAttempts = 5;

        IsRefreshing = true;
        CanPrint = false;

        do
        {
            var response = !string.IsNullOrEmpty(_input)
                ? await _geminiClient.SendRequestAsync(Location.ToString(), _input)
                : await _geminiClient.SendRequestAsync(Location.ToString());

            EntryUrl = $"{response.Uri.Host}{response.Uri.PathAndQuery}";

            switch (response)
            {
                case InputRequiredResponse inputRequired:
                {
                    _input = await DisplayPromptAsync("Input Required", inputRequired.Message);
                    if (string.IsNullOrEmpty(_input))
                        finished = true; // if no user-input was provided, then we cannot continue
                    break;
                }
                case ErrorResponse error:
                {
                    if (remainingAttempts == 1 || !IsRetryAppropriate(error.Status))
                    {
                        await DisplayAlert("Error", error.Message, "OK");
                        finished = true;
                    }
                    else
                        await Task.Delay(1000 / remainingAttempts);

                    remainingAttempts--;
                    break;
                }
                case SuccessfulResponse success:
                {
                    if (!_recentHistory.TryPeek(out var prev) || !prev.Equals(response.Uri))
                        _recentHistory.Push(response.Uri);

                    _settingsDatabase.LastVisitedUrl = response.Uri.ToString();
                    _browsingDatabase.Visited.Add(new Visited
                    {
                        Url = response.Uri.ToString(), Timestamp = DateTime.Now, Title = _pageTitle ?? "(no title)"
                    });

                    if (success is GemtextResponse gemtext)
                        RenderedHtml = RenderGemtextAsHtml(gemtext);
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

                    finished = true;
                    break;
                }
            }
        } while (!finished);

        IsRefreshing = false;
        CanPrint = _printService != null;

        _input = null;
    }

    private async Task LoadDefaultPage()
    {
        var document = new HtmlDocument();
        document.DocumentNode.AppendChild(HtmlNode.CreateNode(_htmlTemplate));

        var body = document.DocumentNode.ChildNodes.FindFirst("main");

        await using (var file = await FileSystem.OpenAppPackageFileAsync("default.html"))
        using (var reader = new StreamReader(file))
        {
            body.AppendChild(HtmlNode.CreateNode(await reader.ReadToEndAsync()));
        }

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

    private double GetExpandedMenuHeight()
    {
        return ExpandableMenu.Sum(element => element.MinimumHeight);
    }

    private void AddMenuAnimations()
    {
        _menuShowAnimation = new Animation(v => ExpandableMenu.HeightRequest = v, 0, GetExpandedMenuHeight(),
            Easing.CubicOut);
        _menuHideAnimation = new Animation(v => ExpandableMenu.HeightRequest = v, GetExpandedMenuHeight(), 0);
    }

    private async void MainPage_OnLoaded(object sender, EventArgs e)
    {
        AddMenuAnimations();
        await LoadPageTemplates();
        Location = _settingsDatabase.LastVisitedUrl?.ToGeminiUri();

        if (Location == null)
            await LoadDefaultPage();
    }

    private async Task LoadPageTemplates()
    {
        await using var template = await FileSystem.OpenAppPackageFileAsync("template.html");
        using var reader = new StreamReader(template);
        _htmlTemplate = await reader.ReadToEndAsync();
    }
}