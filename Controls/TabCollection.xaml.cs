using System.Collections.ObjectModel;
using RosyCrow.Controls.Tabs;
using RosyCrow.Extensions;
using RosyCrow.Views;
using Tab = RosyCrow.Models.Tab;

namespace RosyCrow.Controls;

public partial class TabCollection : ContentView
{
    private Tab _selectedTab;
    private BrowserView _selectedView;

    private ObservableCollection<Tab> _tabs;

    public TabCollection()
    {
        InitializeComponent();

        BindingContext = this;

        Tabs = new ObservableCollection<Tab>();
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

    private void SelectTab(Tab tab)
    {
        SelectedView = tab.View;
        if (_selectedTab != null)
            _selectedTab.Selected = false;
        _selectedTab = tab;
        tab.Selected = true;
    }

    public Task AddDefaultTab()
    {
        return AddTab("rosy-crow://default", "🐦");
    }

    public async Task AddTab(string url, string label)
    {
        var tab = new Tab(url, label)
        {
            Selected = true,
            View = MauiProgram.Services.GetRequiredService<BrowserView>()
        };

        tab.View.ParentPage = this.FindParentPage();
        tab.View.ReadyToShow += (_, _) => SelectTab(tab);
        tab.View.Location = url.ToGeminiUri();

        _tabs.Add(tab);

        await UpdateTabOrder();
    }

    private void BrowserTab_OnSelected(object sender, TabEventArgs e)
    {
        SelectTab(e.Tab);
    }

    private async void AdderTab_OnTriggered(object sender, EventArgs e)
    {
        await AddDefaultTab();
    }

    private async void TabsCollectionView_OnReorderCompleted(object sender, EventArgs e)
    {
        await UpdateTabOrder();
    }

    private async Task UpdateTabOrder()
    {
        for (var i = 0; i < Tabs.Count; i++)
            Tabs[i].Order = i;

        await SaveTabs();
    }

    public Task LoadOrAddDefaultTabs()
    {
        return AddDefaultTab();
    }

    private async Task SaveTabs()
    {
        // var path = Path.Join(FileSystem.CacheDirectory, TabsFileName);
        // await using var file = File.CreateText(path);
        // await file.WriteAsync(JsonConvert.SerializeObject(Tabs.ToArray()));
    }

    private async void BrowserTab_OnRemoveRequested(object sender, TabEventArgs e)
    {
        _tabs.Remove(e.Tab);

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
}