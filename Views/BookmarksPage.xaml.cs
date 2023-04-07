using System.Collections.ObjectModel;
using System.Windows.Input;
using RosyCrow.Interfaces;
using RosyCrow.Models;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class BookmarksPage : ContentPage
{
    private ObservableCollection<Bookmark> _bookmarks;
    private ICommand _loadPage;
    private ICommand _delete;

    private readonly MainPage _mainPage;
    private readonly IBrowsingDatabase _browsingDatabase;

    public BookmarksPage(MainPage mainPage, IBrowsingDatabase browsingDatabase)
    {
        BindingContext = this;

        _mainPage = mainPage;
        _browsingDatabase = browsingDatabase;

        LoadPage = new Command(async param =>
        {
            _mainPage.Browser.Location = new Uri((string)param);
            _mainPage.LoadPageOnAppearing = true;
            await Navigation.PopAsync(true);
        });
        Delete = new Command(async param => await TryDeleteBookmark((int)param));

        InitializeComponent();
    }

    public ObservableCollection<Bookmark> Bookmarks
    {
        get => _bookmarks;
        set
        {
            if (Equals(value, _bookmarks)) return;
            _bookmarks = value;
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

    public ICommand Delete
    {
        get => _delete;
        set
        {
            if (Equals(value, _delete)) return;
            _delete = value;
            OnPropertyChanged();
        }
    }

    private async Task TryDeleteBookmark(int id)
    {
        if (await DisplayAlert("Delete Bookmark", "Are you sure you want to delete this bookmark?", "Yes", "No"))
        {
            var bookmark = Bookmarks.FirstOrDefault(b => b.Id == id);
            if (bookmark == null)
                return;

            Bookmarks.Remove(bookmark);
        }
    }

    private void BookmarksPage_OnAppearing(object sender, EventArgs e)
    {
        Bookmarks = _browsingDatabase.Bookmarks;
    }
}