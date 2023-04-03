using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Maui.Core;
using Yarrow.Extensions;
using Yarrow.Interfaces;
using Yarrow.Models;

namespace Yarrow;

public partial class HistoryPage : ContentPage
{
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly ISettingsDatabase _settingsDatabase;
    private ICommand _clearHistory;

    private ObservableCollection<Visited> _visited;

    public HistoryPage(IBrowsingDatabase browsingDatabase, ISettingsDatabase settingsDatabase)
    {
        InitializeComponent();

        BindingContext = this;

        _browsingDatabase = browsingDatabase;
        _settingsDatabase = settingsDatabase;

        Visited = _browsingDatabase.Visited;
        ClearHistory = new Command(async () => await TryClearHistory());
    }

    private async Task TryClearHistory()
    {
        if (!Visited.Any())
            return;

        if (await DisplayAlert("Clear History", "Are you sure you want to clear your stored history?", "Yes", "No"))
        {
            var deleted = _browsingDatabase.ClearVisited();
            if (deleted > 0)
                this.ShowToast($"{deleted} visited pages deleted", ToastDuration.Short);
        }
    }

    public ObservableCollection<Visited> Visited
    {
        get => _visited;
        set
        {
            if (Equals(value, _visited)) return;
            _visited = value;
            OnPropertyChanged();
        }
    }

    public bool StoreVisited
    {
        get => _settingsDatabase?.SaveVisited ?? default;
        set
        {
            if (value == _settingsDatabase.SaveVisited)
                return;
            _settingsDatabase.SaveVisited = value;
            OnPropertyChanged();
        }
    }

    public ICommand ClearHistory
    {
        get => _clearHistory;
        set
        {
            if (Equals(value, _clearHistory)) return;
            _clearHistory = value;
            OnPropertyChanged();
        }
    }
}