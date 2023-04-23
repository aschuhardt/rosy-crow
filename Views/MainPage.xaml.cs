using System.Windows.Input;
using CommunityToolkit.Maui.Core;
using Microsoft.Maui.Handlers;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Resources.Localization;
#if ANDROID
#endif

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class MainPage : ContentPage
{
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly ISettingsDatabase _settingsDatabase;

    private ICommand _expandMenu;
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
    private ICommand _toggleBookmarked;
    private ICommand _toggleMenuExpanded;

    public MainPage(ISettingsDatabase settingsDatabase, IBrowsingDatabase browsingDatabase)
    {
        InitializeComponent();

        _settingsDatabase = settingsDatabase;
        _browsingDatabase = browsingDatabase;
        _isNavBarVisible = true;

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

        UrlEntry.GestureRecognizers.Add(SwipeDownRecognizer);
        UrlEntry.GestureRecognizers.Add(SwipeUpRecognizer);
        HomeButton.GestureRecognizers.Add(SwipeDownRecognizer);
        HomeButton.GestureRecognizers.Add(SwipeUpRecognizer);
        BookmarkButton.GestureRecognizers.Add(SwipeDownRecognizer);
        BookmarkButton.GestureRecognizers.Add(SwipeUpRecognizer);

        foreach (var button in ExpandableMenu.Children.Cast<Button>())
        {
            button.GestureRecognizers.Add(SwipeUpRecognizer);
            button.GestureRecognizers.Add(new TapGestureRecognizer { Command = button.Command });
        }

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
            if (value == _isNavBarVisible) return;
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

            new Animation(v => NavBar.TranslationY = v, 0, -NavBar.Height * 1.25).Commit(this, "HideNavBar");
        }
    }

    private void PerformMenuAnimations()
    {
        if (IsMenuExpanded)
            _menuShowAnimation.Commit(this, "ShowMenu");
        else
            _menuHideAnimation.Commit(this, "HideMenu", length: 150);
    }

    protected override bool OnBackButtonPressed()
    {
        // if the menu is visible, just close it
        if (IsMenuExpanded)
        {
            IsMenuExpanded = false;
            return true;
        }

        return Browser.GoBack();
    }

    private void OpenMenuItem<T>() where T : ContentPage
    {
        _isMenuExpanded = false;
        _menuHideAnimation.Commit(this, "HideMenu", length: 150,
            finished: async (_, _) => await Navigation.PushPageAsync<T>());
    }

    private void TryLoadHomeUrl()
    {
        if (string.IsNullOrEmpty(_settingsDatabase.HomeUrl))
            this.ShowToast(Text.MainPage_TryLoadHomeUrl_No_home_set, ToastDuration.Long);
        else
            Browser.Location = _settingsDatabase.HomeUrl.ToGeminiUri();
    }

    private void TrySetHomeUrl()
    {
        if (Browser.Location == null)
            return;

        _settingsDatabase.HomeUrl = Browser.Location.ToString();

        Browser.SimulateLocationChanged(); // force buttons to update

        this.ShowToast(Text.MainPage_TrySetHomeUrl_Home_set, ToastDuration.Short);
    }

    private void TryToggleBookmarked()
    {
        if (Browser.Location == null)
            return;

        if (_browsingDatabase.IsBookmark(Browser.Location, out var bookmark))
        {
            _browsingDatabase.Bookmarks.Remove(bookmark);

            Browser.SimulateLocationChanged(); // force buttons to update

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

            this.ShowToast(Text.MainPage_TryToggleBookmarked_Bookmark_added, ToastDuration.Short);
        }
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

    private void MainPage_OnLoaded(object sender, EventArgs e)
    {
        AddMenuAnimations();
        Browser.Location = _settingsDatabase.LastVisitedUrl?.ToGeminiUri();
    }

    private async void MainPage_OnAppearing(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(App.StartupUri))
            Browser.Location = App.StartupUri.ToGeminiUri();

        if (LoadPageOnAppearing)
        {
            LoadPageOnAppearing = false;
            await Browser.LoadPage();
        }
    }
}