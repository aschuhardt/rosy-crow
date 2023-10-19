using System.Diagnostics;
using System.Windows.Input;
using Android.Views;
using CommunityToolkit.Maui.Alerts;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Platforms.Android;
using RosyCrow.Resources.Localization;
using Tab = RosyCrow.Models.Tab;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Controls.Tabs;

public partial class BrowserTab : ContentView
{
    public static readonly BindableProperty ReorderingCommandProperty =
        BindableProperty.Create(nameof(ReorderingCommand), typeof(ICommand), typeof(BrowserTab));

    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly ILogger<BrowserTab> _logger;
    private bool _isReordering;
    private ICommand _tapped;

    public BrowserTab() : this(MauiProgram.Services.GetRequiredService<IBrowsingDatabase>(),
        MauiProgram.Services.GetRequiredService<ILogger<BrowserTab>>())
    {
    }

    public BrowserTab(IBrowsingDatabase browsingDatabase, ILogger<BrowserTab> logger)
    {
        _browsingDatabase = browsingDatabase;
        _logger = logger;

        InitializeComponent();

        ReorderingCommand = new Command<bool>(HandleReordering);
        Tapped = new Command(Select);
    }

    public ICommand Tapped
    {
        get => _tapped;
        set
        {
            if (Equals(value, _tapped)) return;

            _tapped = value;
            OnPropertyChanged();
        }
    }

    public ICommand ReorderingCommand
    {
        get => (ICommand)GetValue(ReorderingCommandProperty);
        set => SetValue(ReorderingCommandProperty, value);
    }

    private void HandleReordering(bool isReordering)
    {
        _isReordering = isReordering;

        if (isReordering)
        {
            Dispatcher.Dispatch(
                async () => await Task.WhenAll(
                    this.ScaleTo(0.85),
                    IconLabel.FadeTo(0.3),
                    DragIndicator.FadeTo(1.0)));
        }
        else
        {
            Dispatcher.Dispatch(
                async () => await Task.WhenAll(
                    this.ScaleTo(1.0),
                    IconLabel.FadeTo(1.0),
                    DragIndicator.FadeTo(0.0)));
        }
    }

    public event EventHandler<TabEventArgs> AfterSelected;
    public event EventHandler<TabEventArgs> RemoveRequested;
    public event EventHandler<TabEventArgs> FetchingIcon;
    public event EventHandler<TabCapsuleEventArgs> ResettingIcon;
    public event EventHandler<TabEventArgs> SettingCustomIcon;
    public event EventHandler ReorderingRequested;
    public event EventHandler RemoveAllRequested;
    public event EventHandler ImportRequested;
    public event EventHandler ExportRequested;

    private void Select()
    {
        // if the button is tapped while selected, then delete it; otherwise, select it
        if (BindingContext is not Tab tab)
            return;

        if (!tab.Selected)
        {
            tab.Selected = true;
            AfterSelected?.Invoke(this, new TabEventArgs(tab));
        }
    }

#if ANDROID
    private void BuildContextMenu(IContextMenu menu)
    {
        if (menu == null)
            return;

        if (BindingContext is not Tab tab)
            return;

        if (_isReordering)
            return;

        var url = tab.Url.ToGeminiUri();
        menu.SetHeaderTitle(tab.Label.IsEmoji() ? url.Host : $"{tab.Label} {url.Host}");

        if (OperatingSystem.IsAndroidVersionAtLeast(28))
            menu.SetGroupDividerEnabled(true);

        if (url.Scheme != Constants.InternalScheme)
        {
            var iconMenu = menu.AddSubMenu("Icon");
            iconMenu?.Add("Fetch (favicon.txt)")?
                .SetOnMenuItemClickListener(new ActionMenuClickHandler(() => FetchingIcon?.Invoke(this, new TabEventArgs(tab))));
            iconMenu?.Add("Set Custom")?
                .SetOnMenuItemClickListener(new ActionMenuClickHandler(() => SettingCustomIcon?.Invoke(this, new TabEventArgs(tab))));

            if (tab.Browser != null && _browsingDatabase.TryGetCapsule(url.Host, out var capsule))
            {
                iconMenu?.Add("Reset")?
                    .SetOnMenuItemClickListener(new ActionMenuClickHandler(() =>
                        ResettingIcon?.Invoke(this, new TabCapsuleEventArgs(tab, capsule))));
            }

            if (_browsingDatabase.TryGetBookmark(tab.Url, out var bookmark))
            {
                menu.Add("Remove Bookmark")?.SetOnMenuItemClickListener(new ActionMenuClickHandler(async () =>
                {
                    _browsingDatabase.Bookmarks.Remove(bookmark);
                    tab.Browser?.SimulateLocationChanged();
                    await Toast.Make(Text.MainPage_TryToggleBookmarked_Bookmark_removed).Show();
                }));
            }
            else if (tab.Browser != null)
            {
                menu.Add("Bookmark")?.SetOnMenuItemClickListener(new ActionMenuClickHandler(async () =>
                {
                    bookmark = new Bookmark { Url = tab.Browser.Location.ToString(), Title = tab.Browser.PageTitle };
                    _browsingDatabase.Bookmarks.Remove(bookmark);
                    tab.Browser?.SimulateLocationChanged();
                    await Toast.Make(Text.MainPage_TryToggleBookmarked_Bookmark_removed).Show();
                }));
            }
        }

        menu.Add("Copy URL")?.SetOnMenuItemClickListener(new ActionMenuClickHandler(async () => await Clipboard.SetTextAsync(tab.Url)));

        var allMenu = menu.AddSubMenu("All Tabs");

        if (OperatingSystem.IsAndroidVersionAtLeast(28))
            allMenu?.SetGroupDividerEnabled(true);

        allMenu?.Add("Import")?
            .SetOnMenuItemClickListener(new ActionMenuClickHandler(() => ImportRequested?.Invoke(this, EventArgs.Empty)));
        allMenu?.Add("Export")?
            .SetOnMenuItemClickListener(new ActionMenuClickHandler(() => ExportRequested?.Invoke(this, EventArgs.Empty)));
        allMenu?.Add(1, IMenu.None, IMenu.None, "Close All")?
            .SetOnMenuItemClickListener(new ActionMenuClickHandler(() => RemoveAllRequested?.Invoke(this, EventArgs.Empty)));
        allMenu?.Add(2, IMenu.None, IMenu.None, "Arrange")?
            .SetOnMenuItemClickListener(new ActionMenuClickHandler(() => ReorderingRequested?.Invoke(this, EventArgs.Empty)));

        menu.Add(1, IMenu.None, IMenu.None, "Close")?
            .SetOnMenuItemClickListener(new ActionMenuClickHandler(() => RemoveRequested?.Invoke(this, new TabEventArgs(tab))));
    }
#endif

    private void SelectButton_OnHandlerChanged(object sender, EventArgs e)
    {
#if ANDROID
        var view = (SelectButton.Handler as ButtonHandler)?.PlatformView;
        if (view == null)
            return;

        view.ContextMenuCreated += (_, arg) => BuildContextMenu(arg.Menu);
#endif

    }
}