using System.Collections.ObjectModel;
using RosyCrow.Models;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class BookmarksPage : ContentPage
{
    private ObservableCollection<Bookmark> _bookmarks;

    public BookmarksPage()
    {
        InitializeComponent();

        BindingContext = this;
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
}