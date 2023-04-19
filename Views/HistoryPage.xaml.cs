using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Maui.Core;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class HistoryPage : ContentPage
{
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly ISettingsDatabase _settingsDatabase;
    private ICommand _clearHistory;
    private int _currentPage;
    private bool _hasNextPage;
    private bool _hasPreviousPage;
    private ICommand _loadPage;
    private ICommand _nextPage;
    private int _pageCount;
    private ICommand _previousPage;

    private ObservableCollection<Visited> _visited;
    private string _pageDescription;
    private bool _shouldShowNavigation;

    public HistoryPage(IBrowsingDatabase browsingDatabase, ISettingsDatabase settingsDatabase, MainPage mainPage)
    {
        BindingContext = this;

        _browsingDatabase = browsingDatabase;
        _settingsDatabase = settingsDatabase;

        NextPage = new Command(() =>
        {
            CurrentPage++;
            LoadCurrentPage();
        });

        PreviousPage = new Command(() =>
        {
            CurrentPage--;
            LoadCurrentPage();
        });

        Visited = new ObservableCollection<Visited>();

        ClearHistory = new Command(async () => await TryClearHistory());
        LoadPage = new Command(async param =>
        {
            mainPage.Browser.Location = new Uri((string)param);
            mainPage.LoadPageOnAppearing = true;
            await Navigation.PopAsync(true);
        });

        InitializeComponent();
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

    public ICommand LoadPage
    {
        get => _loadPage;
        set
        {
            if (Equals(value, _loadPage)) return;
            _loadPage = value;
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

    public ICommand NextPage
    {
        get => _nextPage;
        set
        {
            if (Equals(value, _nextPage)) return;
            _nextPage = value;
            OnPropertyChanged();
        }
    }

    public ICommand PreviousPage
    {
        get => _previousPage;
        set
        {
            if (Equals(value, _previousPage)) return;
            _previousPage = value;
            OnPropertyChanged();
        }
    }

    public bool ShouldShowNavigation
    {
        get => _shouldShowNavigation;
        set
        {
            if (value == _shouldShowNavigation) return;
            _shouldShowNavigation = value;
            OnPropertyChanged();
        }
    }

    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            if (value == _currentPage) return;
            _currentPage = value;
            OnPropertyChanged();
        }
    }

    public bool HasNextPage
    {
        get => _hasNextPage;
        set
        {
            if (value == _hasNextPage) return;
            _hasNextPage = value;
            OnPropertyChanged();
        }
    }

    public bool HasPreviousPage
    {
        get => _hasPreviousPage;
        set
        {
            if (value == _hasPreviousPage) return;
            _hasPreviousPage = value;
            OnPropertyChanged();
        }
    }

    public string PageDescription
    {
        get => _pageDescription;
        set
        {
            if (value == _pageDescription) return;
            _pageDescription = value;
            OnPropertyChanged();
        }
    }

    public int PageCount
    {
        get => _pageCount;
        set
        {
            if (value == _pageCount) return;
            _pageCount = value;
            OnPropertyChanged();
        }
    }

    private async Task TryClearHistory()
    {
        if (!Visited.Any())
            return;

        if (await DisplayAlert("Clear History", "Are you sure you want to clear your stored history?", "Yes", "No"))
        {
            var deleted = _browsingDatabase.ClearVisited();
            LoadCurrentPage();
            if (deleted > 0)
                this.ShowToast($"{deleted} visited pages deleted", ToastDuration.Short);
        }
    }

    private void LoadCurrentPage()
    {
        if (Visited == null)
            return;

        Visited.Clear();

        var page = _browsingDatabase.GetVisitedPage(CurrentPage, out var lastPage);

        foreach (var entry in page)
            Visited.Add(entry);

        HasNextPage = !lastPage;
        HasPreviousPage = CurrentPage > 1;
        PageCount = _browsingDatabase.GetVisitedPageCount();
        PageDescription = $" Page {CurrentPage} of {PageCount}";
        ShouldShowNavigation = HasNextPage || HasPreviousPage;
    }

    private void HistoryPage_OnAppearing(object sender, EventArgs e)
    {
        CurrentPage = 1;
        PageCount = _browsingDatabase.GetVisitedPageCount();
        LoadCurrentPage();
    }
}