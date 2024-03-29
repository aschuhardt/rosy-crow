using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Resources.Localization;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class HistoryPage : ContentPage
{
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly ISettingsDatabase _settingsDatabase;
    private readonly ILogger<HistoryPage> _logger;
    private ICommand _clearHistory;
    private int _currentPage;
    private bool _hasNextPage;
    private bool _hasPreviousPage;
    private ICommand _loadPage;
    private ICommand _nextPage;
    private int _pageCount;
    private string _pageDescription;
    private ICommand _previousPage;
    private bool _shouldShowNavigation;

    private ObservableCollection<Visited> _visited;

    public HistoryPage(IBrowsingDatabase browsingDatabase, ISettingsDatabase settingsDatabase, MainPage mainPage, ILogger<HistoryPage> logger)
    {
        _browsingDatabase = browsingDatabase;
        _settingsDatabase = settingsDatabase;
        _logger = logger;

        InitializeComponent();

        BindingContext = this;

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
            mainPage.TabCollection.SelectedTab.Location = new Uri((string)param);
            mainPage.LoadPageOnAppearing = true;
            await Navigation.PopAsync(true);
        });
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
        try
        {
            if (!Visited.Any())
                return;

            if (await DisplayAlert(Text.HistoryPage_TryClearHistory_Clear_History, Text.HistoryPage_TryClearHistory_Confirm,
                    Text.Global_Yes, Text.Global_No))
            {
                var deleted = _browsingDatabase.ClearVisited();
                LoadCurrentPage();
                if (deleted > 0)
                {
                    this.ShowToast(string.Format(Text.HistoryPage_TryClearHistory__0__visited_pages_deleted, deleted),
                        ToastDuration.Short);
                }

                _logger.LogInformation(@"Cleared the {Count} visited page entries", deleted);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while attempting to clear the visited page history");
        }
    }

    private void LoadCurrentPage()
    {
        try
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
            PageDescription = string.Format(Text.HistoryPage_LoadCurrentPage__Page__0__of__1_, CurrentPage, PageCount);
            ShouldShowNavigation = HasNextPage || HasPreviousPage;
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while loading a page of previously-visited URIs");
        }
    }

    private void HistoryPage_OnAppearing(object sender, EventArgs e)
    {
        CurrentPage = 1;
        PageCount = _browsingDatabase.GetVisitedPageCount();
        LoadCurrentPage();
    }
}