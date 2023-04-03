using System.Windows.Input;
using CommunityToolkit.Maui.Core;
using Microsoft.Maui.Handlers;
using Yarrow.Extensions;
using Yarrow.Interfaces;
using Yarrow.Models;
#if ANDROID
#endif

// ReSharper disable AsyncVoidLambda

namespace Yarrow;

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
        LoadEnteredUrl = new Command<string>(url => Browser.Location = url.ToGeminiUri());
        ToggleMenuExpanded = new Command(() => IsMenuExpanded = !IsMenuExpanded);
        HideMenu = new Command(() => IsMenuExpanded = false);
        ExpandMenu = new Command(() => IsMenuExpanded = true);
        Print = new Command(() => Browser.Print());
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

        return Browser.GoBack();
    }

    private void TryLoadHomeUrl()
    {
        if (string.IsNullOrEmpty(_settingsDatabase.HomeUrl))
            this.ShowToast("No home URL has been set. Long-press the Home button to set one.", ToastDuration.Long);
        else
            Browser.Location = _settingsDatabase.HomeUrl.ToGeminiUri();
    }

    private void TrySetHomeUrl()
    {
        if (Browser.Location == null)
            return;

        _settingsDatabase.HomeUrl = Browser.Location.ToString();

        OnPropertyChanged(nameof(Location)); // force buttons to update

        this.ShowToast("Home set", ToastDuration.Short);
    }

    private void TryToggleBookmarked()
    {
        if (Browser.Location == null)
            return;

        if (_browsingDatabase.IsBookmark(Browser.Location, out var bookmark))
        {
            _browsingDatabase.Bookmarks.Remove(bookmark);

            OnPropertyChanged(nameof(Location)); // force buttons to update

            this.ShowToast("Bookmark removed", ToastDuration.Short);
        }
        else
        {
            _browsingDatabase.Bookmarks.Add(new Bookmark
                { Title = Browser.PageTitle, Url = Browser.Location.ToString() });

            OnPropertyChanged(nameof(Location)); // force buttons to update

            this.ShowToast("Bookmark added", ToastDuration.Short);
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

    private async void MainPage_OnLoaded(object sender, EventArgs e)
    {
        AddMenuAnimations();
        Browser.Location = _settingsDatabase.LastVisitedUrl?.ToGeminiUri();

        if (Browser.Location == null)
            await Browser.LoadDefaultPage();
    }
}