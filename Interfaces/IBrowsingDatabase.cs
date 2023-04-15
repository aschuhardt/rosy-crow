using System.Collections.ObjectModel;
using System.ComponentModel;
using RosyCrow.Models;

namespace RosyCrow.Interfaces;

public interface IBrowsingDatabase : INotifyPropertyChanged
{
    ObservableCollection<Bookmark> Bookmarks { get; set; }
    ObservableCollection<Visited> Visited { get; set; }
    ObservableCollection<Identity> Identities { get; set; }

    bool IsBookmark(Uri uri, out Bookmark found);
    int ClearVisited();
}