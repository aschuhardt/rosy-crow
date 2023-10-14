using System.Collections.ObjectModel;
using RosyCrow.Controls.Tabs;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Views;
using Tab = RosyCrow.Models.Tab;

namespace RosyCrow.Controls;

public partial class TabCollection : ContentView
{
    private readonly IBrowsingDatabase _browsingDatabase;

    private Tab _selectedTab;
    private BrowserView _selectedView;
    private ObservableCollection<Tab> _tabs;

    public TabCollection() : this(MauiProgram.Services.GetRequiredService<IBrowsingDatabase>())
    {
    }

    public TabCollection(IBrowsingDatabase browsingDatabase)
    {
        _browsingDatabase = browsingDatabase;

        InitializeComponent();

        BindingContext = this;

        Tabs = _browsingDatabase.Tabs;
    }

    public BrowserView SelectedView
    {
        get => _selectedView;
        set
        {
            if (Equals(value, _selectedView)) return;

            _selectedView = value;
            SelectedViewChanged?.Invoke(this, EventArgs.Empty);
            OnPropertyChanged();
        }
    }

    public ObservableCollection<Tab> Tabs
    {
        get => _tabs;
        set
        {
            if (Equals(value, _tabs))
                return;

            _tabs = value;
            OnPropertyChanged();
        }
    }

    public event EventHandler SelectedViewChanged;

    public void SelectTab(Tab tab)
    {
        if (tab.View == null)
        {
            // the tab was just loaded from the database and hasn't been initialized yet
            InitializeTab(tab);

            // when the tab is ready to be shown, it will raise ReadyToShow which
            // will call this method again
            return;
        }

        SelectedView = tab.View;

        if (_selectedTab != null)
        {
            _selectedTab.Selected = false;
            _browsingDatabase.Update(_selectedTab);
        }

        _selectedTab = tab;
        tab.Selected = true;
        _browsingDatabase.Update(tab);
    }

    public Task AddDefaultTab()
    {
        return AddTab("rosy-crow://default", "🐦");
    }

    public Task AddTab(Uri uri)
    {
        return AddTab(uri.ToString(), uri.Host[..1]);
    }

    public Task AddTab(string url, string label)
    {
        var tab = new Tab(url, label)
        {
            Selected = true
        };

        InitializeTab(tab);
        Tabs.Add(tab);

        return _browsingDatabase.UpdateTabOrder();
    }

    private void InitializeTab(Tab tab)
    {
        tab.View = MauiProgram.Services.GetRequiredService<BrowserView>();
        tab.View.ParentPage = this.FindParentPage();
        tab.View.ReadyToShow += (_, _) => SelectTab(tab);
        tab.View.Location = tab.Url.ToGeminiUri();
        tab.View.PageLoaded += (_, _) => UpdateTabUrl(tab);
    }

    private static void UpdateTabUrl(Tab tab)
    {
        var location = tab.View.Location;
        tab.Url = location.ToString();
        tab.Label = location.Host[..1];
    }

    private void BrowserTab_OnSelected(object sender, TabEventArgs e)
    {
        SelectTab(e.Tab);
    }

    private async void AdderTab_OnTriggered(object sender, EventArgs e)
    {
        await AddDefaultTab();
    }

    private async void BrowserTab_OnRemoveRequested(object sender, TabEventArgs e)
    {
        Tabs.Remove(e.Tab);

        if (!Tabs.Any())
        {
            await AddDefaultTab();
        }
        else
        {
            // try to find the tab that is to the left of this one; if there is none, just select the first one
            var next = Tabs.OrderByDescending(t => t.Order).FirstOrDefault(t => t.Order < e.Tab.Order);
            SelectTab(next ?? _tabs.First());
        }
    }

    private async void TabsCollectionView_OnReorderCompleted(object sender, EventArgs e)
    {
        await _browsingDatabase.UpdateTabOrder();
    }

    private void TabCollection_OnLoaded(object sender, EventArgs e)
    {
        if (Tabs.FirstOrDefault(t => t.Selected) is { } tab)
            SelectTab(tab);
    }
}