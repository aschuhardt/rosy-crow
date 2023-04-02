using System.Windows.Input;

// ReSharper disable AsyncVoidLambda

namespace Yarrow;

public partial class BookmarksPage : ContentPage
{
    public BookmarksPage()
    {
        InitializeComponent();

        BindingContext = this;
    }
}