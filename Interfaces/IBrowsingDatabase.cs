using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using Opal.Tofu;
using RosyCrow.Models;

namespace RosyCrow.Interfaces;

public interface IBrowsingDatabase : INotifyPropertyChanged, ICertificateDatabase
{
    ObservableCollection<Bookmark> Bookmarks { get; set; }
    ObservableCollection<Identity> Identities { get; set; }

    bool IsBookmark(Uri uri, out Bookmark found);
    int ClearVisited();
    int GetVisitedPageCount();
    IEnumerable<Visited> GetVisitedPage(int page, out bool lastPage);
    void AddVisitedPage(Visited visited);
    void SetHostCertificate(string host, X509Certificate2 certificate);
    bool TryGetHostCertificate(string host, out HostCertificate certificate);
}