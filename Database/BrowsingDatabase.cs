using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using LiteDB;
using Opal.Tofu;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;

namespace RosyCrow.Database;

internal class BrowsingDatabase : IBrowsingDatabase
{
    private readonly ILiteCollection<Bookmark> _bookmarksStore;
    private readonly ILiteCollection<HostCertificate> _hostCertificates;
    private readonly ILiteCollection<Identity> _identityStore;
    private readonly ISettingsDatabase _settingsDatabase;
    private readonly ILiteCollection<Visited> _visitedStore;

    private ObservableCollection<Bookmark> _bookmarks;
    private ObservableCollection<Identity> _identities;

    public BrowsingDatabase(ILiteDatabase database, ISettingsDatabase settingsDatabase)
    {
        _settingsDatabase = settingsDatabase;

        _bookmarksStore = database.GetCollection<Bookmark>();
        _bookmarksStore.EnsureIndex(b => b.Url);

        _hostCertificates = database.GetCollection<HostCertificate>();
        _hostCertificates.EnsureIndex(c => c.Host, true);

        _visitedStore = database.GetCollection<Visited>();
        _identityStore = database.GetCollection<Identity>();

        Bookmarks = new ObservableCollection<Bookmark>(_bookmarksStore.Query().OrderBy(b => b.Title ?? b.Url).ToList());
        Identities = new ObservableCollection<Identity>(_identityStore.Query().OrderBy(i => i.Name).ToList());

        var activeIdentityId = _settingsDatabase.ActiveIdentityId ?? -1;
        foreach (var identity in Identities)
            identity.IsActive = identity.Id == activeIdentityId;
    }

    public ObservableCollection<Identity> Identities
    {
        get => _identities;
        set
        {
            if (Equals(value, _identities))
                return;

            if (_identities != null)
                _identities.CollectionChanged -= Identities_CollectionChanged;

            _identities = value;
            Identities.CollectionChanged += Identities_CollectionChanged;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<Bookmark> Bookmarks
    {
        get => _bookmarks;
        set
        {
            if (Equals(value, _bookmarks))
                return;

            if (_bookmarks != null)
                _bookmarks.CollectionChanged -= Bookmarks_CollectionChanged;

            _bookmarks = value;
            _bookmarks.CollectionChanged += Bookmarks_CollectionChanged;
            OnPropertyChanged();
        }
    }

    public bool IsBookmark(Uri location, out Bookmark found)
    {
        found = _bookmarks.FirstOrDefault(b =>
        {
            var bookmarkUrl = b.Url.ToGeminiUri();
            return bookmarkUrl.Host.Equals(location.Host, StringComparison.InvariantCultureIgnoreCase) &&
                   bookmarkUrl.PathAndQuery.Equals(location.PathAndQuery);
        });

        return found != null;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public int ClearVisited()
    {
        return _visitedStore.DeleteAll();
    }

    public int GetVisitedPageCount()
    {
        return Math.Max(1, (int)Math.Ceiling(_visitedStore.Count() / (double)_settingsDatabase.HistoryPageSize));
    }

    public void AddVisitedPage(Visited visited)
    {
        _visitedStore.Insert(visited);
    }

    public void SetHostCertificate(string host, X509Certificate2 certificate)
    {
        _hostCertificates.Insert(new HostCertificate
        {
            Added = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
            Expiration = certificate.NotAfter,
            Fingerprint = certificate.Thumbprint,
            Subject = certificate.Subject,
            Issuer = certificate.Issuer,
            Host = host.ToLowerInvariant()
        });
    }

    public bool TryGetHostCertificate(string host, out HostCertificate certificate)
    {
        host = host.ToLowerInvariant();
        certificate = _hostCertificates.FindOne(c => c.Host == host);
        return certificate != null;
    }

    public IEnumerable<Visited> GetVisitedPage(int page, out bool lastPage)
    {
        var pageSize = _settingsDatabase.HistoryPageSize;
        var result = _visitedStore.Query()
            .OrderByDescending(v => v.Timestamp)
            .Skip((page - 1) * pageSize).Limit(pageSize)
            .ToList();

        lastPage = result.Count < pageSize;

        return result;
    }

    public bool IsCertificateValid(string host, X509Certificate certificate, out InvalidCertificateReason result)
    {
        host = host.ToLowerInvariant();

        // Opal *should* be returning this version under the hood
        var cert = certificate as X509Certificate2 ?? new X509Certificate2(certificate);

        if (!cert.MatchesHostname(host))
        {
            result = InvalidCertificateReason.NameMismatch;
            return false;
        }

        if (DateTime.UtcNow > cert.NotAfter)
        {
            result = InvalidCertificateReason.Expired;
            return false;
        }

        if (DateTime.UtcNow < cert.NotBefore)
        {
            result = InvalidCertificateReason.NotYet;
            return false;
        }

        if (!TryGetHostCertificate(host, out var stored))
        {
            // never seen this one; add a new entry for it
            SetHostCertificate(host, cert);
            result = InvalidCertificateReason.Other;
            return true;
        }

        if (stored.Fingerprint != cert.Thumbprint)
        {
            // this is a different one from what we remember
            result = InvalidCertificateReason.TrustedMismatch;
            return false;
        }

        result = default;
        return true;
    }

    public void RemoveTrusted(string host)
    {
        host = host.ToLowerInvariant();
        var stored = _hostCertificates.FindOne(c => c.Host == host);
        if (stored != null)
            _hostCertificates.Delete(stored.Id);
    }

    private void Identities_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems != null:
                foreach (var entity in e.NewItems.Cast<Identity>())
                    entity.Id = _identityStore.Insert(entity);
                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems != null:
                foreach (var entity in e.OldItems.Cast<Identity>())
                    _identityStore.Delete(entity.Id);
                break;
            case NotifyCollectionChangedAction.Replace:
            case NotifyCollectionChangedAction.Move:
                throw new NotImplementedException();
            case NotifyCollectionChangedAction.Reset:
                var activeId = _settingsDatabase.ActiveIdentityId ?? -1;
                foreach (var identity in _identityStore.Query().OrderBy(i => i.Name).ToList())
                {
                    identity.IsActive = identity.Id == activeId;
                    _identities.Add(identity);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void Bookmarks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems != null:
                foreach (var entity in e.NewItems.Cast<Bookmark>())
                    entity.Id = _bookmarksStore.Insert(entity);
                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems != null:
                foreach (var entity in e.OldItems.Cast<Bookmark>())
                    _bookmarksStore.Delete(entity.Id);
                break;
            case NotifyCollectionChangedAction.Replace:
            case NotifyCollectionChangedAction.Move:
                throw new NotImplementedException();
            case NotifyCollectionChangedAction.Reset:
                foreach (var bookmark in _bookmarksStore.Query().OrderBy(b => b.Title ?? b.Url).ToList())
                    _bookmarks.Add(bookmark);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}