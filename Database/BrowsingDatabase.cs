﻿using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Opal.Tofu;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using SQLite;
using Tab = RosyCrow.Models.Tab;

namespace RosyCrow.Database;

internal class BrowsingDatabase : IBrowsingDatabase
{
    private readonly ISettingsDatabase _settingsDatabase;
    private readonly ILogger<BrowsingDatabase> _logger;
    private readonly SQLiteConnection _database;

    private ObservableCollection<Bookmark> _bookmarks;
    private ObservableCollection<Identity> _identities;
    private ObservableCollection<Tab> _tabs;

    public BrowsingDatabase(ISettingsDatabase settingsDatabase, ILogger<BrowsingDatabase> logger, SQLiteConnection database)
    {
        _settingsDatabase = settingsDatabase;
        _logger = logger;
        _database = database;

        _database.CreateTables(
            CreateFlags.None,
            typeof(Bookmark),
            typeof(Identity),
            typeof(Visited),
            typeof(HostCertificate),
            typeof(Tab),
            typeof(Capsule));

        Bookmarks = new ObservableCollection<Bookmark>(_database.Table<Bookmark>().OrderBy(b => b.Order).ToList());
        Identities = new ObservableCollection<Identity>(_database.Table<Identity>().OrderBy(i => i.Name).ToList());
        Tabs = new ObservableCollection<Tab>(_database.Table<Tab>().OrderBy(t => t.Order).ToList());

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

    public ObservableCollection<Tab> Tabs
    {
        get => _tabs;
        set
        {
            if (Equals(value, _tabs)) return;

            if (_tabs != null)
                _tabs.CollectionChanged -= Tabs_CollectionChanged;

            _tabs = value;
            _tabs.CollectionChanged += Tabs_CollectionChanged;
            OnPropertyChanged();
        }
    }

    public bool TryGetBookmark(Uri location, out Bookmark found)
    {
        found = _bookmarks.FirstOrDefault(b => b.Url.AreGeminiUrlsEqual(location));
        return found != null;
    }

    public bool TryGetBookmark(string uri, out Bookmark found)
    {
        found = _bookmarks.FirstOrDefault(b => b.Url.AreGeminiUrlsEqual(uri));
        return found != null;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public int ClearVisited()
    {
        _logger.LogDebug(@"Clearing visited page history");

        try
        {
            return _database.DeleteAll<Visited>();
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while clearing visited page history");
            return 0;
        }
    }

    public int GetVisitedPageCount()
    {
        var count = Math.Max(1, (int)Math.Ceiling(_database.Table<Visited>().Count() / (double)_settingsDatabase.HistoryPageSize));
        _logger.LogDebug(@"{Count} pages visited", count);
        return count;
    }

    public void AddVisitedPage(Visited visited)
    {
        _logger.LogDebug(@"Storing visited page {URI}", visited.Url);

        try
        {
            _database.Insert(visited);
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while inserting a visited page");
        }
    }

    public void SetHostCertificate(string host, X509Certificate2 certificate, bool accepted)
    {
        try
        {
            _database.Insert(new HostCertificate
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
            _logger.LogError(e, @"Exception thrown while inserting a host certificate");
        }
    }

    public bool TryGetHostCertificate(string host, out HostCertificate certificate)
    {
        _logger.LogDebug(@"Checking for a stored certificate for host {Host}", host);

        try
        {
            host = host.ToLowerInvariant();
            certificate = _database.Table<HostCertificate>().FirstOrDefault(c => c.Host == host);
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while performing a host certificate lookup");
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
            var result = _database.Table<Visited>()
                .OrderByDescending(v => v.Timestamp)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToList();

            lastPage = result.Count < pageSize;

            _logger.LogDebug(@"Found {Count} visited page entries on page {Page}.  Last page: {LastPage}", result.Count, page, lastPage);

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while getting visited page history");
            lastPage = true;
            return Enumerable.Empty<Visited>();
        }
    }

    public void InsertOrReplace<T>(T obj)
    {
        var affected = _database.InsertOrReplace(obj, typeof(T));
        _logger.LogDebug(@"Inserted or replaced {Count} object of type {Type}", affected, typeof(T).Name);
    }

    public void Update<T>(T obj)
    {
        var affected = _database.Update(obj, typeof(T));
        _logger.LogDebug(@"Updated {Count} object of type {Type}", affected, typeof(T).Name);
    }

    public void UpdateAll<T>(params T[] entities)
    {
        var affected = _database.UpdateAll(entities);
        _logger.LogDebug(@"Updated {Count} objects of type {Type} (in bulk)", affected, typeof(T).Name);
    }

    public bool TryGetCapsule(string hostname, out Capsule capsule)
    {
        capsule = _database.Table<Capsule>().FirstOrDefault(c => c.Host == hostname);
        return capsule != null;
    }

    public void RemoveHostCertificate(string host)
    {
        if (!TryGetHostCertificate(host, out var cert))
        {
            _logger.LogWarning(@"Cannot removed stored certificate metadata for unknown host {Host}", host);
            return;
        }

        _logger.LogDebug(@"Removing certificate metadata for host {Host}", host);

        try
        {
            _database.Delete(cert);
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while removing a host certificate");
        }
    }

    public bool IsCertificateValid(string host, X509Certificate certificate, out InvalidCertificateReason result)
    {
        // Opal *should* be returning this version under the hood
        var cert = certificate as X509Certificate2 ?? new X509Certificate2(certificate);

        _logger.LogDebug(@"Verifying certificate with fingerprint {Fingerprint} for {Host}", cert.Thumbprint, host);

        if (!cert.MatchesHostname(host))
        {
            _logger.LogInformation(@"The name on certificate from {Host} does not match the host's name (found subject {Subject})", host, cert.SubjectName);

            result = InvalidCertificateReason.NameMismatch;
            return false;
        }

        if (DateTime.UtcNow > cert.NotAfter)
        {
            _logger.LogInformation(@"The certificate from {Host} expired as of {NotAfter}", host, cert.NotAfter);

            result = InvalidCertificateReason.Expired;
            return false;
        }

        if (DateTime.UtcNow < cert.NotBefore)
        {
            _logger.LogInformation(@"The certificate from {Host} is not valid until {NotBefore}", host, cert.NotBefore);

            result = InvalidCertificateReason.NotYet;
            return false;
        }

        if (!TryGetHostCertificate(host, out var stored))
        {
            _logger.LogInformation(@"Received an unrecognized certificate from {Host}; It will be stored", host);

            // never seen this one; add a new entry for it
            SetHostCertificate(host, cert, !_settingsDatabase.StrictTofuMode);

            if (_settingsDatabase.StrictTofuMode)
            {
                _logger.LogInformation(@"The certificate from {Host} needs to be be manually accepted by the user", host);

                result = InvalidCertificateReason.TrustedMismatch;
                return false;
            }

            _logger.LogDebug(@"The certificate from {Host} is valid and will be trusted", host);

            result = InvalidCertificateReason.Other;
            return true;
        }

        if (stored == null)
        {
            _logger.LogInformation(@"Failed to load the stored certificate for {Host} to validate against", host);

            result = InvalidCertificateReason.TrustedMismatch;
            return false;
        }

        if (!stored.Accepted || stored.Fingerprint != cert.Thumbprint)
        {
            // this is a different one from what we remember
            if (!stored.Accepted)
                _logger.LogInformation(@"The certificate from {Host} has yet to be manually accepted by the user", host);
            else
                _logger.LogInformation(@"The certificate from {Host} does not match the version previously stored", host);

            result = InvalidCertificateReason.TrustedMismatch;
            return false;
        }

        _logger.LogDebug(@"The certificate received from {Host} matches what is stored", host);

        result = default;
        return true;
    }

    public Task UpdateTabOrder()
    {
        return Task.Run(() =>
        {
            for (var i = 0; i < _tabs.Count; i++)
                _tabs[i].Order = i;

            var affected = _database.UpdateAll(_tabs);
            _logger.LogInformation(@"{Count} tabs re-ordered", affected);
        });
    }

    public Task UpdateBookmarkOrder()
    {
        return Task.Run(() =>
        {
            for (var i = 0; i < _bookmarks.Count; i++)
                _bookmarks[i].Order = i;

            var affected = _database.UpdateAll(_bookmarks);
            _logger.LogInformation(@"{Count} bookmarks re-ordered", affected);
        });
    }

    private void Identities_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems != null:
                foreach (var entity in e.NewItems.Cast<Identity>())
                    _database.Insert(entity);
                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems != null:
                foreach (var entity in e.OldItems.Cast<Identity>())
                    _database.Delete(entity);
                break;
            case NotifyCollectionChangedAction.Replace:
            case NotifyCollectionChangedAction.Move:
                throw new NotImplementedException();
            case NotifyCollectionChangedAction.Reset:
                var count = _database.DeleteAll<Identity>();
                _logger.LogInformation(@"Deleted {Count} identities", count);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(e));
        }
    }

    private void Bookmarks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems != null:
                foreach (var entity in e.NewItems.Cast<Bookmark>())
                    _database.Insert(entity);
                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems != null:
                foreach (var entity in e.OldItems.Cast<Bookmark>())
                    _database.Delete(entity);
                break;
            case NotifyCollectionChangedAction.Replace:
            case NotifyCollectionChangedAction.Move:
                throw new NotImplementedException();
            case NotifyCollectionChangedAction.Reset:
                var count = _database.DeleteAll<Bookmark>();
                _logger.LogInformation(@"Deleted {Count} bookmarks", count);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(e));
        }
    }

    private void Tabs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewItems != null:
                foreach (var entity in e.NewItems.Cast<Tab>())
                    _database.Insert(entity);
                break;
            case NotifyCollectionChangedAction.Remove when e.OldItems != null:
                foreach (var entity in e.OldItems.Cast<Tab>())
                    _database.Delete(entity);
                break;
            case NotifyCollectionChangedAction.Replace:
            case NotifyCollectionChangedAction.Move:
                throw new NotImplementedException();
            case NotifyCollectionChangedAction.Reset:
                var count = _database.DeleteAll<Tab>();
                _logger.LogInformation(@"Deleted {Count} tabs", count);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(e));
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}