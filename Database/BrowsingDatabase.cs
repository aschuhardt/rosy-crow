using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using LiteDB;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<BrowsingDatabase> _logger;

    private ObservableCollection<Bookmark> _bookmarks;
    private ObservableCollection<Identity> _identities;

    public BrowsingDatabase(ILiteDatabase database, ISettingsDatabase settingsDatabase, ILogger<BrowsingDatabase> logger)
    {
        _settingsDatabase = settingsDatabase;
        _logger = logger;

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
        _logger.LogDebug("Clearing visited page history");

        try
        {
            return _visitedStore.DeleteAll();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while clearing visited page history");
            return 0;
        }
    }

    public int GetVisitedPageCount()
    {
        var count = Math.Max(1, (int)Math.Ceiling(_visitedStore.Count() / (double)_settingsDatabase.HistoryPageSize));
        _logger.LogDebug("{Count} pages visited", count);
        return count;
    }

    public void AddVisitedPage(Visited visited)
    {
        _logger.LogDebug("Storing visited page {URI}", visited.Url);

        try
        {
            _visitedStore.Insert(visited);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while inserting a visited page");
        }
    }

    public void SetHostCertificate(string host, X509Certificate2 certificate, bool accepted)
    {
        try
        {
            _hostCertificates.Insert(new HostCertificate
            {
                Added = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                Expiration = certificate.NotAfter,
                Fingerprint = certificate.Thumbprint,
                Subject = certificate.Subject,
                Issuer = certificate.Issuer,
                Host = host.ToLowerInvariant(),
                Accepted = accepted
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while inserting a host certificate");
        }
    }

    public bool TryGetHostCertificate(string host, out HostCertificate certificate)
    {
        _logger.LogDebug("Checking for a stored certificate for host {Host}", host);

        try
        {
            host = host.ToLowerInvariant();
            certificate = _hostCertificates.FindOne(c => c.Host == host);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while performing a host certificate lookup");
            certificate = null;
            return false;
        }

        return certificate != null;
    }

    public IEnumerable<Visited> GetVisitedPage(int page, out bool lastPage)
    {
        try
        {
            var pageSize = _settingsDatabase.HistoryPageSize;
            var result = _visitedStore.Query()
                .OrderByDescending(v => v.Timestamp)
                .Skip((page - 1) * pageSize).Limit(pageSize)
                .ToList();

            lastPage = result.Count < pageSize;

            _logger.LogDebug("Found {Count} visited page entries on page {Page}.  Last page: {LastPage}", result.Count, page, lastPage);

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while getting visited page history");
            lastPage = true;
            return Enumerable.Empty<Visited>();
        }
    }

    public void AcceptHostCertificate(string host)
    {
        if (!TryGetHostCertificate(host, out var cert))
        {
            _logger.LogWarning("Cannot accept a host certificate for {Host} that has not yet been stored", host);
            return;
        }

        _logger.LogDebug("Accepting new certificate for host {Host}", host);

        cert.Accepted = true;

        try
        {
            _hostCertificates.Update(cert);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while accepting a new host certificate");
        }
    }

    public bool IsCertificateValid(string host, X509Certificate certificate, out InvalidCertificateReason result)
    {
        // Opal *should* be returning this version under the hood
        var cert = certificate as X509Certificate2 ?? new X509Certificate2(certificate);

        _logger.LogDebug("Verifying certificate with fingerprint {Fingerprint} for {Host}", cert.Thumbprint, host);

        if (!cert.MatchesHostname(host))
        {
            _logger.LogInformation("The name on certificate from {Host} does not match the host's name (found subject {Subject})", host, cert.SubjectName);

            result = InvalidCertificateReason.NameMismatch;
            return false;
        }

        if (DateTime.UtcNow > cert.NotAfter)
        {
            _logger.LogInformation("The certificate from {Host} expired as of {NotAfter}", host, cert.NotAfter);

            result = InvalidCertificateReason.Expired;
            return false;
        }

        if (DateTime.UtcNow < cert.NotBefore)
        {
            _logger.LogInformation("The certificate from {Host} is not valid until {NotBefore}", host, cert.NotBefore);

            result = InvalidCertificateReason.NotYet;
            return false;
        }

        if (!TryGetHostCertificate(host, out var stored))
        {
            _logger.LogInformation("Received an unrecognized certificate from {Host}; It will be stored", host);

            // never seen this one; add a new entry for it
            SetHostCertificate(host, cert, !_settingsDatabase.StrictTofuMode);

            if (_settingsDatabase.StrictTofuMode)
            {
                _logger.LogInformation("The certificate from {Host} needs to be be manually accepted by the user", host);

                result = InvalidCertificateReason.TrustedMismatch;
                return false;
            }

            _logger.LogDebug("The certificate from {Host} is valid and will be trusted", host);

            result = InvalidCertificateReason.Other;
            return true;
        }

        if (stored == null)
        {
            _logger.LogInformation("Failed to load the stored certificate for {Host} to validate against", host);

            result = InvalidCertificateReason.TrustedMismatch;
            return false;
        }

        if (!stored.Accepted || stored.Fingerprint != cert.Thumbprint)
        {
            // this is a different one from what we remember
            if (!stored.Accepted)
                _logger.LogInformation("The certificate from {Host} has yet to be manually accepted by the user", host);
            else
                _logger.LogInformation("The certificate from {Host} does not match the version previously stored", host);

            result = InvalidCertificateReason.TrustedMismatch;
            return false;
        }

        _logger.LogDebug("The certificate received from {Host} matches what is stored", host);

        result = default;
        return true;
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