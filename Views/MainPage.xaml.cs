using System.Windows.Input;
using Android.Views;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Resources.Localization;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class MainPage : ContentPage
{
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly ILogger<MainPage> _logger;
    private readonly ISettingsDatabase _settingsDatabase;

    private ICommand _expandMenu;
    private ICommand _findInPage;
    private ICommand _findNextInPage;
    private ICommand _hideMenu;
    private bool _isMenuExpanded;
    private bool _isNavBarVisible;
    private ICommand _loadEnteredUrl;
    private ICommand _loadHomeUrl;
    private bool _loadPageOnAppearing;
    private Animation _menuHideAnimation;
    private Animation _menuShowAnimation;
    private ICommand _openBookmarks;
    private ICommand _openHistory;
    private ICommand _openIdentity;
    private ICommand _openSettings;
    private ICommand _print;
    private ICommand _setHomeUrl;
    private ICommand _showPageCertificate;
    private ICommand _toggleBookmarked;
    private ICommand _toggleMenuExpanded;

    public MainPage(ISettingsDatabase settingsDatabase, IBrowsingDatabase browsingDatabase, ILogger<MainPage> logger)
    {
        _settingsDatabase = settingsDatabase;
        _browsingDatabase = browsingDatabase;
        _logger = logger;
        _isNavBarVisible = true;

        InitializeComponent();

        BindingContext = this;

        LoadEnteredUrl = new Command<string>(url =>
            Browser.Location = url.StartsWith(Constants.InternalScheme) ? new Uri(url) : url.ToGeminiUri());
        ToggleMenuExpanded = new Command(() => IsMenuExpanded = !IsMenuExpanded);
        HideMenu = new Command(() => IsMenuExpanded = false);
        ExpandMenu = new Command(() => IsMenuExpanded = true);
        Print = new Command(() => Browser.Print());
        LoadHomeUrl = new Command(TryLoadHomeUrl);
        SetHomeUrl = new Command(TrySetHomeUrl);
        ToggleBookmarked = new Command(TryToggleBookmarked);
        OpenBookmarks = new Command(OpenMenuItem<BookmarksPage>);
        OpenHistory = new Command(OpenMenuItem<HistoryPage>);
        OpenIdentity = new Command(OpenMenuItem<IdentityPage>);
        OpenSettings = new Command(OpenMenuItem<SettingsPage>);
        FindInPage = new Command(async () => await TryFindInPage());
        FindNextInPage = new Command(() => Browser.FindTextInPage(Browser.FindNextQuery));
        ShowPageCertificate = new Command(OpenMenuItem<CertificatePage>);

        UrlEntry.GestureRecognizers.Add(SwipeDownRecognizer);
        UrlEntry.GestureRecognizers.Add(SwipeUpRecognizer);
        HomeButton.GestureRecognizers.Add(SwipeDownRecognizer);
        HomeButton.GestureRecognizers.Add(SwipeUpRecognizer);
        BookmarkButton.GestureRecognizers.Add(SwipeDownRecognizer);
        BookmarkButton.GestureRecognizers.Add(SwipeUpRecognizer);

        foreach (var button in ExpandableMenu.Children.Where(v => v is Button).Cast<Button>())
        {
            button.GestureRecognizers.Add(SwipeUpRecognizer);
            button.GestureRecognizers.Add(new TapGestureRecognizer { Command = button.Command });
        }

        UrlEntry.HandlerChanged += SetupUrlEnterHandling;

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
#endif
        });
    }

    public bool LoadPageOnAppearing
    {
        get => _loadPageOnAppearing;
        set
        {
            if (value == _loadPageOnAppearing) return;
            _loadPageOnAppearing = value;
            OnPropertyChanged();
        }
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
            // don't allow the navbar to be hidden if "Find in page" is active
            if (value == _isNavBarVisible || (!value && Browser.HasFindNextQuery))
                return;
            _isNavBarVisible = value;
            OnPropertyChanged();
            PerformNavBarAnimations();
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

    public ICommand FindInPage
    {
        get => _findInPage;
        set
        {
            if (Equals(value, _findInPage)) return;
            _findInPage = value;
            OnPropertyChanged();
        }
    }

    public ICommand FindNextInPage
    {
        get => _findNextInPage;
        set
        {
            if (Equals(value, _findNextInPage)) return;
            _findNextInPage = value;
            OnPropertyChanged();
        }
    }

    public ICommand ShowPageCertificate
    {
        get => _showPageCertificate;
        set
        {
            if (Equals(value, _showPageCertificate)) return;
            _showPageCertificate = value;
            OnPropertyChanged();
        }
    }

    private void SetupUrlEnterHandling(object sender, EventArgs e)
    {
        if (sender is not Entry entry || entry.Handler is not EntryHandler handler)
            return;

#if ANDROID
        handler.PlatformView.KeyPress += (_, args) =>
        {
            if (args.Event is { KeyCode: Keycode.Enter or Keycode.NumpadEnter, Action: KeyEventActions.Up })
            {
                entry.ReturnCommand?.Execute(entry.ReturnCommandParameter);
                args.Handled = true;
            }

            args.Handled = false;
        };
#endif
    }

    private void PerformNavBarAnimations()
    {
        try
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

                new Animation(v => NavBar.TranslationY = v, 0, -NavBar.Height * 1.25).Commit(this, "HideNavBar");
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while performing navbar animation");
        }
    }

    private void PerformMenuAnimations()
    {
        try
        {
            if (IsMenuExpanded)
                _menuShowAnimation.Commit(this, "ShowMenu");
            else
                _menuHideAnimation.Commit(this, "HideMenu", length: 150);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while performing menu animation");
        }
    }

    protected override bool OnBackButtonPressed()
    {
        try
        {
            // if the menu is visible, just close it
            if (IsMenuExpanded)
            {
                IsMenuExpanded = false;
                return true;
            }

            return Browser.GoBack();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while navigating backward");
            return false;
        }
    }

    private void OpenMenuItem<T>() where T : ContentPage
    {
        try
        {
            if (IsMenuExpanded)
            {
                // collapse and then navigate
                _isMenuExpanded = false;
                _menuHideAnimation.Commit(this, "HideMenu", length: 150,
                    finished: async (_, _) =>
                    {
                        await NavBar.FadeTo(0, 100);
                        await Navigation.PushPageAsync<T>();
                    });
            }
            else
            {
                Dispatcher.Dispatch(async () =>
                {
                    await NavBar.FadeTo(0, 100);
                    await Navigation.PushPageAsync<T>();
                });
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while opening menu item {Name}", typeof(T).Name);
        }
    }

    private void TryLoadHomeUrl()
    {
        try
        {
            _logger.LogDebug("Attempting to load the home URI");

            if (string.IsNullOrEmpty(_settingsDatabase.HomeUrl))
                this.ShowToast(Text.MainPage_TryLoadHomeUrl_No_home_set, ToastDuration.Long);
            else
                Browser.Location = _settingsDatabase.HomeUrl.ToGeminiUri();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while navigating to the home URI");
        }
    }

    private void TrySetHomeUrl()
    {
        if (Browser.Location == null)
            return;

        try
        {
            _settingsDatabase.HomeUrl = Browser.Location.ToString();

            _logger.LogInformation("Home URI set to {URI}", _settingsDatabase.HomeUrl);

            Browser.SimulateLocationChanged(); // force buttons to update

            this.ShowToast(Text.MainPage_TrySetHomeUrl_Home_set, ToastDuration.Short);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while setting the home URI to {URI}", Browser.Location);
        }
    }

    private async Task TryFindInPage()
    {
        try
        {
            string query;
            if (Browser.HasFindNextQuery)
            {
                query = await DisplayPromptAsync(Text.MainPage_TryFindInPage_Find_in_Page,
                    Text.MainPage_TryFindInPage_OngoingPrompt,
                    initialValue: Browser.FindNextQuery);
            }
            else
            {
                query = await DisplayPromptAsync(Text.MainPage_TryFindInPage_Find_in_Page,
                    Text.MainPage_TryFindInPage_InitialPrompt);
            }

            if (string.IsNullOrEmpty(query))
            {
                if (Browser.HasFindNextQuery)
                    Browser.ClearFindResults();
                return;
            }

            IsMenuExpanded = false;

            Browser.FindTextInPage(query);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown attempting to find text in the page");
        }
    }

    private void TryToggleBookmarked()
    {
        if (Browser.Location == null)
            return;

        try
        {
            if (_browsingDatabase.IsBookmark(Browser.Location, out var bookmark))
            {
                _browsingDatabase.Bookmarks.Remove(bookmark);
                Browser.SimulateLocationChanged(); // force buttons to update

                _logger.LogInformation("Removing bookmarked location {URI}", bookmark.Url);

                this.ShowToast(Text.MainPage_TryToggleBookmarked_Bookmark_removed, ToastDuration.Short);
            }
            else
            {
                _browsingDatabase.Bookmarks.Add(new Bookmark
                {
                    Title = Browser.PageTitle ?? Browser.Location.Segments.LastOrDefault() ?? Browser.Location.Host,
                    Url = Browser.Location.ToString()
                });
                Browser.SimulateLocationChanged(); // force buttons to update

                _logger.LogInformation("Set bookmarked location {URI}", bookmark.Url);

                this.ShowToast(Text.MainPage_TryToggleBookmarked_Bookmark_added, ToastDuration.Short);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown toggling the bookmark for {URI}", Browser.Location);
        }
    }

    private double GetExpandedMenuHeight()
    {
        return ExpandableMenu.Sum(e =>
            (double.IsNaN(e.MinimumHeight) ? 0 : e.MinimumHeight) + e.Margin.VerticalThickness);
    }

    private void AddMenuAnimations()
    {
        _menuShowAnimation = new Animation(v => ExpandableMenu.HeightRequest = v, 0, GetExpandedMenuHeight(),
            Easing.CubicOut);
        _menuHideAnimation = new Animation(v => ExpandableMenu.HeightRequest = v, GetExpandedMenuHeight(), 0);
    }

    private void MainPage_OnLoaded(object sender, EventArgs e)
    {
        try
        {
            AddMenuAnimations();
            Browser.Location = _settingsDatabase.LastVisitedUrl?.ToGeminiUri();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Exception thrown on MainPage loaded");
        }
    }

    private async void MainPage_OnAppearing(object sender, EventArgs e)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(App.StartupUri))
                Browser.Location = App.StartupUri.ToGeminiUri();

            await NavBar.FadeTo(1, 100);

            if (LoadPageOnAppearing)
            {
                LoadPageOnAppearing = false;
                await Browser.LoadPage();
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Exception thrown on MainPage appearing");
        }
    }
}