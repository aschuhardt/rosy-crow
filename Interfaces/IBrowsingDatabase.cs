using System.Collections.ObjectModel;
using System.ComponentModel;
using RosyCrow.Models;

namespace RosyCrow.Interfaces;

public interface IBrowsingDatabase : INotifyPropertyChanged
{
    ObservableCollection<Bookmark> Bookmarks { get; set; }
    ObservableCollection<Identity> Identities { get; set; }

    bool IsBookmark(Uri uri, out Bookmark found);
    int ClearVisited();
    int GetVisitedPageCount();
    IEnumerable<Visited> GetVisitedPage(int page, out bool lastPage);
    void AddVisitedPage(Visited visited);
}