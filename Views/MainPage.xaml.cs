using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Android.Content.Res;
using Android.Views;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Handlers.Items;
using Microsoft.Maui.Handlers;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Resources.Localization;
using Color = Android.Graphics.Color;
using Tab = RosyCrow.Models.Tab;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class MainPage : ContentPage
{
    public static readonly BindableProperty CurrentTabProperty = BindableProperty.Create(nameof(CurrentTab), typeof(Tab), typeof(MainPage));

    public static readonly BindableProperty TabsProperty =
        BindableProperty.Create(nameof(Tabs), typeof(ObservableCollection<Tab>), typeof(MainPage));

    public static readonly BindableProperty CurrentTabViewTemplateProperty =
        BindableProperty.Create(nameof(CurrentTabViewTemplate), typeof(DataTemplate), typeof(MainPage));

    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly ILogger<MainPage> _logger;
    private readonly ISettingsDatabase _settingsDatabase;
    private readonly IDispatcherTimer _swipeTimer;

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
    private bool _pullTabVisible;
    private ICommand _setHomeUrl;
    private ICommand _showPageCertificate;
    private bool _tabsEnabled;
    private ICommand _toggleBookmarked;
    private ICommand _toggleMenuExpanded;
    private bool _whatsNewShown;

    public MainPage(ISettingsDatabase settingsDatabase, IBrowsingDatabase browsingDatabase, ILogger<MainPage> logger)
    {
        _settingsDatabase = settingsDatabase;
        _browsingDatabase = browsingDatabase;
        _logger = logger;
        _isNavBarVisible = true;
        _settingsDatabase.PropertyChanged += SettingsChanged;
        TabsEnabled = _settingsDatabase.TabsEnabled;

        _swipeTimer = Dispatcher.CreateTimer();
        _swipeTimer.Interval = TimeSpan.FromMilliseconds(500);
        _swipeTimer.IsRepeating = false;
        _swipeTimer.Tick += (_, _) =>
        {
            if (Carousel != null)
                Carousel.IsSwipeEnabled = TabsEnabled && _settingsDatabase.SwipeEnabled;
        };

        InitializeComponent();

        BindingContext = this;

        Carousel.IsSwipeEnabled = TabsEnabled && _settingsDatabase.SwipeEnabled;
        LoadEnteredUrl = new Command<string>(async url => await TryLoadEnteredUrl(url));
        ToggleMenuExpanded = new Command(() => IsMenuExpanded = !IsMenuExpanded);
        HideMenu = new Command(() => IsMenuExpanded = false);
        ExpandMenu = new Command(() => IsMenuExpanded = true);
        LoadHomeUrl = new Command(TryLoadHomeUrl);
        SetHomeUrl = new Command(TrySetHomeUrl);
        ToggleBookmarked = new Command(TryToggleBookmarked);
        OpenBookmarks = new Command(OpenMenuItem<BookmarksPage>);
        OpenHistory = new Command(OpenMenuItem<HistoryPage>);
        OpenIdentity = new Command(OpenMenuItem<IdentityPage>);
        OpenSettings = new Command(OpenMenuItem<SettingsPage>);
        FindInPage = new Command(async () => await TryFindInPage());
        Print = new Command(() =>
        {
            if (CurrentTab.Print?.CanExecute(null) ?? false)
                CurrentTab.Print.Execute(null);
        });
        FindNextInPage = new Command(() =>
        {
            if (CurrentTab.FindNext?.CanExecute(CurrentTab.FindNextQuery) ?? false)
                CurrentTab.FindNext.Execute(CurrentTab.FindNextQuery);
        });
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

        WebViewHandler.Mapper.AppendToMapping(@"WebViewScrollingAware",
            (handler, _) =>
            {
#if ANDROID
                handler.PlatformView.ScrollChange += (sender, args) =>
                {
                    if (args.ScrollY > args.OldScrollY + 5)
                        IsNavBarVisible = false;
                    else if (args.ScrollY < args.OldScrollY - 20 || args.ScrollY == 0)
                        IsNavBarVisible = true;

                    if (TabsEnabled)
                    {
                        Carousel.IsSwipeEnabled = false;
                        _swipeTimer.Stop();
                        _swipeTimer.Start();
                    }
                };
#endif
            });

        EntryHandler.Mapper.AppendToMapping(@"HideUnderline",
            (handler, _) =>
            {
#if ANDROID
                handler.PlatformView.BackgroundTintList = ColorStateList.ValueOf(Color.Transparent);
#endif
            });

        EditorHandler.Mapper.AppendToMapping(@"HideUnderline",
            (handler, _) =>
            {
#if ANDROID
                handler.PlatformView.BackgroundTintList = ColorStateList.ValueOf(Color.Transparent);
#endif
            });
    }

    public Tab CurrentTab
    {
        get => (Tab)GetValue(CurrentTabProperty);
        set
        {
            // don't overwrite the selected tab while the user is rearranging tabs
            if (TabCollection.IsReordering)
                return;

            // don't try setting the selected tab until the tab collection has had
            // a chance to load and select the appropriate target
            if (TabCollection.SelectedTab == null)
                return;

            SetValue(CurrentTabProperty, value);
        }
    }

    public ObservableCollection<Tab> Tabs
    {
        get => (ObservableCollection<Tab>)GetValue(TabsProperty);
        set => SetValue(TabsProperty, value);
    }

    public DataTemplate CurrentTabViewTemplate
    {
        get => (DataTemplate)GetValue(CurrentTabViewTemplateProperty);
        set => SetValue(CurrentTabViewTemplateProperty, value);
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

    public bool TabsEnabled
    {
        get => _tabsEnabled;
        set
        {
            if (value == _tabsEnabled) return;

            _tabsEnabled = value;
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
            if (value == _isNavBarVisible || !value && CurrentTab.HasFindNextQuery)
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

    public bool PullTabVisible
    {
        get => _pullTabVisible;
        set
        {
            if (value == _pullTabVisible) return;

            _pullTabVisible = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     For some reason setting the Tabs property is when reordering has ended is triggering Carousel.OnCurrentItemChanged.
    ///     Don't actually overwrite the selected tab unless we've updated the other things as well.
    /// </summary>
    private bool ShouldUpdateSelectedTab
    {
        // this is silly and bad, but I can't prevent 'Tabs = __' from triggering an update on Carousel.CurrentItem.
        // at least I know that the thing won't be inexplicably changing its opacity.
        // maybe i'll find another solution for this sometime.
        get => Carousel.Opacity > 0;
    }

    private void SettingsChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ISettingsDatabase.TabsEnabled) or nameof(ISettingsDatabase.SwipeEnabled))
        {
            TabsEnabled = _settingsDatabase.TabsEnabled;
            Carousel.IsSwipeEnabled = TabsEnabled && _settingsDatabase.SwipeEnabled;
        }
    }

    private async Task TryLoadEnteredUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            url = $@"{Constants.InternalScheme}://default";

        UrlEntry.Unfocus();

        try
        {
            CurrentTab.Location = url.StartsWith(Constants.InternalScheme)
                ? new Uri(url)
                : url.ToGeminiUri();
        }
        catch (UriFormatException)
        {
            _logger.LogError(@"Invalid URL entered: ""{URL}""", url);
            await Toast.Make(Text.MainPage_MainPage_That_address_is_invalid).Show();
        }
    }

    private void UrlEntry_HandlerChanged(object sender, EventArgs e)
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
                {
                    await Task.WhenAny(
                        NavBar.TranslateTo(NavBar.TranslationX, 0),
                        PullTab.TranslateTo(PullTab.TranslationX, 0),
                        TabCollection.TranslateTo(TabCollection.TranslationX, 0));
                });
            }
            else
            {
                // also collapse the menu if the navbar is being hidden
                if (IsMenuExpanded)
                    IsMenuExpanded = false;

                Dispatcher.Dispatch(async () =>
                {
                    //await PullTab.FadeTo(0, 100);
                    await Task.WhenAny(
                        PullTab.TranslateTo(PullTab.TranslationX, -(PullTab.Y + PullTab.Height * 1.25)),
                        NavBar.TranslateTo(NavBar.TranslationX, -NavBar.Height * 1.25),
                        TabCollection.TranslateTo(TabCollection.TranslationX, TabCollection.TranslationY + TabCollection.Height * 1.25));
                });
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while performing navbar animation");
        }
    }

    private void PerformMenuAnimations()
    {
        try
        {
            if (IsMenuExpanded)
                _menuShowAnimation.Commit(this, @"ShowMenu");
            else
                _menuHideAnimation.Commit(this, @"HideMenu", length: 150);
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while performing menu animation");
        }
    }

    protected override bool OnBackButtonPressed()
    {
        try
        {
            if (UrlEntry.IsFocused)
            {
                UrlEntry.Unfocus();
                return true;
            }

            if (IsMenuExpanded)
            {
                IsMenuExpanded = false;
                return true;
            }

            if (TabCollection.IsReordering)
            {
                TabCollection.IsReordering = false;
                return true;
            }

            if (CurrentTab?.GoBack.CanExecute(null) ?? false)
            {
                CurrentTab.GoBack.Execute(null);
                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while navigating backward");
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
                _menuHideAnimation.Commit(this,
                    @"HideMenu",
                    length: 150,
                    finished: async (_, _) =>
                    {
                        await Task.WhenAny(
                            NavBar.FadeTo(0, 100),
                            PullTab.FadeTo(0, 100));
                        await Navigation.PushPageAsync<T>();
                    });
            }
            else
            {
                Dispatcher.Dispatch(async () =>
                {
                    await Task.WhenAny(
                        NavBar.FadeTo(0, 100),
                        PullTab.FadeTo(0, 100));
                    await Navigation.PushPageAsync<T>();
                });
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while opening menu item {Name}", typeof(T).Name);
        }
    }

    private void TryLoadHomeUrl()
    {
        try
        {
            _logger.LogDebug(@"Attempting to load the home URI");

            if (string.IsNullOrEmpty(_settingsDatabase.HomeUrl))
                this.ShowToast(Text.MainPage_TryLoadHomeUrl_No_home_set, ToastDuration.Long);
            else
                CurrentTab.Location = _settingsDatabase.HomeUrl.ToGeminiUri();
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while navigating to the home URI");
        }
    }

    private void TrySetHomeUrl()
    {
        if (CurrentTab.Location == null)
            return;

        try
        {
            _settingsDatabase.HomeUrl = CurrentTab.Location.ToString();

            _logger.LogInformation(@"Home URI set to {URI}", _settingsDatabase.HomeUrl);

            CurrentTab.OnBookmarkChanged(); // force buttons to update

            this.ShowToast(Text.MainPage_TrySetHomeUrl_Home_set, ToastDuration.Short);
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while setting the home URI to {URI}", CurrentTab.Location);
        }
    }

    private async Task TryFindInPage()
    {
        try
        {
            string query;

            if (CurrentTab.HasFindNextQuery)
            {
                query = await DisplayPromptAsync(Text.MainPage_TryFindInPage_Find_in_Page,
                    Text.MainPage_TryFindInPage_OngoingPrompt,
                    initialValue: CurrentTab.FindNextQuery);
            }
            else
            {
                query = await DisplayPromptAsync(Text.MainPage_TryFindInPage_Find_in_Page,
                    Text.MainPage_TryFindInPage_InitialPrompt);
            }

            if (string.IsNullOrEmpty(query))
            {
                if (CurrentTab?.ClearFind.CanExecute(null) ?? false)
                    CurrentTab.ClearFind.Execute(null);
                return;
            }

            IsMenuExpanded = false;

            if (CurrentTab?.FindNext.CanExecute(CurrentTab.FindNextQuery) ?? false)
                CurrentTab.FindNext.Execute(CurrentTab.FindNextQuery);
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown attempting to find text in the page");
        }
    }

    private void TryToggleBookmarked()
    {
        if (CurrentTab.Location == null)
            return;

        try
        {
            if (_browsingDatabase.TryGetBookmark(CurrentTab.Location, out var bookmark))
            {
                _browsingDatabase.Bookmarks.Remove(bookmark);
                CurrentTab.OnBookmarkChanged(); // force buttons to update

                _logger.LogInformation(@"Removing bookmarked location {URI}", bookmark.Url);

                this.ShowToast(Text.MainPage_TryToggleBookmarked_Bookmark_removed, ToastDuration.Short);
            }
            else
            {
                _browsingDatabase.Bookmarks.Add(new Bookmark
                {
                    Title = CurrentTab.Title ?? CurrentTab.Location.Segments.LastOrDefault() ?? CurrentTab.Location.Host,
                    Url = CurrentTab.Location.ToString()
                });

                CurrentTab.OnBookmarkChanged(); // force buttons to update

                _logger.LogInformation(@"Set bookmarked location {URI}", bookmark.Url);

                this.ShowToast(Text.MainPage_TryToggleBookmarked_Bookmark_added, ToastDuration.Short);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown toggling the bookmark for {URI}", CurrentTab.Location);
        }
    }

    private double GetExpandedMenuHeight()
    {
        return ExpandableMenu.Sum(e =>
            (double.IsNaN(e.MinimumHeight)
                ? 0
                : e.MinimumHeight) + e.Margin.VerticalThickness);
    }

    private void AddMenuAnimations()
    {
        _menuShowAnimation = new Animation(v => ExpandableMenu.HeightRequest = v,
            0,
            GetExpandedMenuHeight(),
            Easing.CubicOut);
        _menuHideAnimation = new Animation(v => ExpandableMenu.HeightRequest = v, GetExpandedMenuHeight(), 0);
    }

    private async void MainPage_OnLoaded(object sender, EventArgs e)
    {
        try
        {
            AddMenuAnimations();

            TabCollection.ParentPage = this;

            if (!TabCollection.Tabs.Any())
            {
                if (!string.IsNullOrWhiteSpace(_settingsDatabase.LastVisitedUrl) && 
                    _settingsDatabase.LastVisitedUrl.ToGeminiUri() is { Scheme: Constants.GeminiScheme } geminiUri)
                {
                    await TabCollection.AddTab(geminiUri);
                }
                else
                {
                    await TabCollection.AddDefaultTab();
                }
            }

            PullTabVisible = !_settingsDatabase.HidePullTab;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, @"Exception thrown on MainPage loaded");
        }
    }

    private async void MainPage_OnAppearing(object sender, EventArgs e)
    {
        try
        {
            PullTabVisible = !_settingsDatabase.HidePullTab;

            await Task.WhenAny(
                NavBar.FadeTo(1, 100),
                PullTab.FadeTo(1, 100));

            if (LoadPageOnAppearing)
            {
                LoadPageOnAppearing = false;
                if (CurrentTab?.Load.CanExecute(null) ?? false)
                    CurrentTab.Load.Execute(null);
            }

            if (!_whatsNewShown && VersionTracking.IsFirstLaunchForCurrentVersion)
            {
                await Navigation.PushPageAsync<WhatsNewPage>();
                _whatsNewShown = true;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, @"Exception thrown on MainPage appearing");
        }
    }

    private void UrlEntry_OnFocused(object sender, FocusEventArgs e)
    {
        if (TabCollection.IsReordering)
            TabCollection.IsReordering = false;

        HomeButton.IsVisible = false;
        PageInfoButton.IsVisible = false;
        BookmarkButton.IsVisible = false;
    }

    private void UrlEntry_OnUnfocused(object sender, FocusEventArgs e)
    {
        HomeButton.IsVisible = true;
        PageInfoButton.IsVisible = true;
        BookmarkButton.IsVisible = true;
    }

    private void Tabs_SelectedTabChanged(object sender, EventArgs e)
    {
        IsNavBarVisible = true;
        if (UrlEntry.IsFocused)
            UrlEntry.Unfocus();
    }
}