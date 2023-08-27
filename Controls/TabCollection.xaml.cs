using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Tab = RosyCrow.Models.Tab;

namespace RosyCrow.Controls;

public partial class TabCollection : ContentView
{
    public static BindableProperty SelectedIdProperty =
        BindableProperty.Create(nameof(SelectedId), typeof(Guid), typeof(TabCollection));

    private Guid _selectedId;
    private Tab _selected;
    private ObservableCollection<Tab> _tabs;

    public event EventHandler NewTabRequested;

    public TabCollection()
    {
        InitializeComponent();

        BindingContext = this;

        Tabs = new ObservableCollection<Tab>();
        Tabs.CollectionChanged += Tabs_CollectionChanged;
    }

    private void Tabs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                break;
            case NotifyCollectionChangedAction.Remove:
                break;
            case NotifyCollectionChangedAction.Replace:
                break;
            case NotifyCollectionChangedAction.Move:
                break;
            case NotifyCollectionChangedAction.Reset:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public Guid SelectedId
    {
        get => _selectedId;
        set
        {
            if (value.Equals(_selectedId))
                return;

            _selectedId = value;
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

    public void AddTab(string url, char label)
    {
        var tab = new Tab(url, label);

        // if this is the first tab, select it
        if (!_tabs.Any())
        {
            SelectTab(tab);
        }

        _tabs.Add(tab);
    }

    public void AddTab(string url, string title)
    {
        AddTab(url, title[0]);
    }

    public void UpdateTab(string url, string title)
    {
        if (_selected == null)
            throw new InvalidOperationException("Tried to update a tab, but no tab is open");

        _selected.Url = url;
        _selected.Label = title[0];
    }

    private void BrowserTab_OnSelected(object sender, Tab selected)
    {
        SelectTab(selected);
    }

    private void SelectTab(Tab tab)
    {
        // deselect the prior
        if (_selected != null)
            _selected.Selected = false;

        tab.Selected = true;

        _selected = tab;
        SelectedId = _selected.Id;
    }

    private void AdderTab_OnTriggered(object sender, EventArgs e)
    {
        NewTabRequested?.Invoke(this, EventArgs.Empty);
    }
}