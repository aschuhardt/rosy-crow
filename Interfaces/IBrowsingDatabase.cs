using System.Collections.ObjectModel;
using System.ComponentModel;
using Opal.Tofu;
using RosyCrow.Models;
using Tab = RosyCrow.Models.Tab;

namespace RosyCrow.Interfaces;

public interface IBrowsingDatabase : INotifyPropertyChanged, ICertificateDatabase
{
    ObservableCollection<Bookmark> Bookmarks { get; set; }
    ObservableCollection<Identity> Identities { get; set; }
    ObservableCollection<Tab> Tabs { get; set; }

    bool TryGetBookmark(Uri uri, out Bookmark found);
    bool TryGetBookmark(string uri, out Bookmark found);
    int ClearVisited();
    int GetVisitedPageCount();
    IEnumerable<Visited> GetVisitedPage(int page, out bool lastPage);
    void AddVisitedPage(Visited visited);
    bool TryGetHostCertificate(string host, out HostCertificate certificate);
    void AcceptHostCertificate(string host);
    Task UpdateBookmarkOrder();
    Task UpdateTabOrder();
    void Update<T>(T obj);
    void UpdateAll<T>(params T[] entities);
    bool TryGetCapsule(string hostname, out Capsule capsule);
    void InsertOrReplace<T>(T obj);
}