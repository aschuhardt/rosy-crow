using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiteDB;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;

namespace RosyCrow.Database;

internal class BrowsingDatabase : IBrowsingDatabase
{
    private readonly ILiteCollection<Bookmark> _bookmarksStore;
    private readonly ILiteCollection<Identity> _identityStore;
    private readonly ILiteCollection<Visited> _visitedStore;
    private readonly ISettingsDatabase _settingsDatabase;

    private ObservableCollection<Bookmark> _bookmarks;
    private ObservableCollection<Identity> _identities;

    public BrowsingDatabase(ILiteDatabase database, ISettingsDatabase settingsDatabase)
    {
        _settingsDatabase = settingsDatabase;
        _bookmarksStore = database.GetCollection<Bookmark>();
        _bookmarksStore.EnsureIndex(b => b.Url);

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