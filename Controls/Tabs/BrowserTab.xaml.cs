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

public partial class BrowserTab : TabButtonBase
{
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly ILogger<BrowserTab> _logger;

    public BrowserTab() : this(MauiProgram.Services.GetRequiredService<IBrowsingDatabase>(),
        MauiProgram.Services.GetRequiredService<ILogger<BrowserTab>>())
    {
    }

    public BrowserTab(IBrowsingDatabase browsingDatabase, ILogger<BrowserTab> logger) : base(true)
    {
        _browsingDatabase = browsingDatabase;
        _logger = logger;
        InitializeComponent();
    }

    public event EventHandler<TabEventArgs> AfterSelected;
    public event EventHandler<TabEventArgs> RemoveRequested;
    public event EventHandler<TabEventArgs> FetchingIcon;
    public event EventHandler<TabCapsuleEventArgs> ResettingIcon;
    public event EventHandler<TabEventArgs> SettingCustomIcon;

    private void BrowserTab_OnBindingContextChanged(object sender, EventArgs e)
    {
        if (BindingContext == null)
            return;

        if (BindingContext is not Tab tab)
            throw new InvalidOperationException(
                $"BrowserTab should only be bound to a {nameof(Tab)}; {BindingContext.GetType().Name} was bound instead!");

        tab.SelectedChanged = new Command<Tab>(t => HandleSelectionChanged(t.Selected));

        HandleSelectionChanged(tab.Selected);
    }

    public override void Tapped()
    {
        // if the button is tapped while selected, then delete it; otherwise, select it
        if (BindingContext is not Tab tab)
            return;

        if (!tab.Selected)
        {
            tab.Selected = true;
            AfterSelected?.Invoke(this, new TabEventArgs(tab));
        }
        else
        {
            RemoveRequested?.Invoke(this, new TabEventArgs(tab));
        }
    }

#if ANDROID
    private void BuildContextMenu(IContextMenu menu)
    {
        if (menu == null)
            return;

        if (BindingContext is not Tab tab)
            return;

        var url = tab.Url.ToGeminiUri();
        menu.SetHeaderTitle(url.Host);

        if (url.Scheme != Constants.InternalScheme)
        {
            var iconMenu = menu.AddSubMenu("Icon");
            iconMenu?.Add("Fetch (favicon.txt)")?
                .SetOnMenuItemClickListener(new ActionMenuClickHandler(() => FetchingIcon?.Invoke(this, new TabEventArgs(tab))));
            iconMenu?.Add("Set Custom")?
                .SetOnMenuItemClickListener(new ActionMenuClickHandler(() => SettingCustomIcon?.Invoke(this, new TabEventArgs(tab))));

            if (tab.View != null && _browsingDatabase.TryGetCapsule(url.Host, out var capsule))
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
                    tab.View?.SimulateLocationChanged();
                    await Toast.Make(Text.MainPage_TryToggleBookmarked_Bookmark_removed).Show();
                }));
            }
            else if (tab.View != null)
            {
                menu.Add("Bookmark")?.SetOnMenuItemClickListener(new ActionMenuClickHandler(async () =>
                {
                    bookmark = new Bookmark { Url = tab.View.Location.ToString(), Title = tab.View.PageTitle };
                    _browsingDatabase.Bookmarks.Remove(bookmark);
                    tab.View?.SimulateLocationChanged();
                    await Toast.Make(Text.MainPage_TryToggleBookmarked_Bookmark_removed).Show();
                }));
            }
        }

        menu.Add("Copy URL")?.SetOnMenuItemClickListener(new ActionMenuClickHandler(async () => await Clipboard.SetTextAsync(tab.Url)));
    }
#endif

    private void BrowserTab_OnHandlerChanged(object sender, EventArgs e)
    {
#if ANDROID
        var view = (Handler as ContentViewHandler)?.PlatformView;
        if (view == null)
            return;

        view.ContextMenuCreated += (_, arg) => BuildContextMenu(arg.Menu);
#endif
    }
}