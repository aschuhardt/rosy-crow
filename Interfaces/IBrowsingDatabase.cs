using System.Collections.ObjectModel;
using System.ComponentModel;
using Yarrow.Models;

namespace Yarrow.Interfaces;

public interface IBrowsingDatabase : INotifyPropertyChanged
{
    ObservableCollection<Bookmark> Bookmarks { get; set; }
    ObservableCollection<Visited> Visited { get; set; }

    bool IsBookmark(Uri uri, out Bookmark found);
}