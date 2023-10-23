using System.Collections.ObjectModel;
using System.Windows.Input;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Resources.Localization;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class BookmarksPage : ContentPage
{
    private readonly IBrowsingDatabase _browsingDatabase;

    private readonly MainPage _mainPage;
    private ObservableCollection<Bookmark> _bookmarks;
    private ICommand _delete;
    private ICommand _loadPage;

    public BookmarksPage(MainPage mainPage, IBrowsingDatabase browsingDatabase)
    {
        _mainPage = mainPage;
        _browsingDatabase = browsingDatabase;

        InitializeComponent();

        BindingContext = this;

        LoadPage = new Command(async param =>
        {
            _mainPage.CurrentTab.Location = new Uri((string)param);
            _mainPage.LoadPageOnAppearing = true;
            await Navigation.PopAsync(true);
        });
        Delete = new Command(async param => await TryDeleteBookmark((int)param));
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
        if (await DisplayAlert(Text.BookmarksPage_TryDeleteBookmark_Delete_Bookmark,
                Text.BookmarksPage_TryDeleteBookmark_Confirm, Text.Global_Yes, Text.Global_No))
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

    private async void ReorderableItemsView_OnReorderCompleted(object sender, EventArgs e)
    {
        await _browsingDatabase.UpdateBookmarkOrder();
    }
}