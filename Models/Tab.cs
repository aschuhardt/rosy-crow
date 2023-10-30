using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using RosyCrow.Controls.Tabs;
using SQLite;

namespace RosyCrow.Models;

public partial class Tab : INotifyPropertyChanged
{
    private bool _canPrint;
    private bool _canShowHostCertificate;
    private ICommand _clearFind;
    private ICommand _findNext;
    private string _findNextQuery;
    private ICommand _goBack;
    private ICommand _handleReordering;
    private string _html;
    private string _input;
    private bool _isRefreshing;
    private string _label;
    private ICommand _load;
    private Uri _location;
    private ICommand _print;
    private ICommand _refresh;
    private string _renderUrl;
    private bool _selected;
    private string _title;
    private Stack<Uri> _recentHistory;

    [GeneratedRegex("([^\\p{P}\\p{Z}\\p{Cc}\\p{Cf}\\p{Co}\\p{Cn}]){1,2}", RegexOptions.CultureInvariant)]
    private static partial Regex DefaultLabelPattern();

    public Tab()
    {
        RecentHistory = new Stack<Uri>();
    }

    public Tab(string url, string label) : this()
    {
        Url = url;
        Label = label;
    }

    public Tab(Uri uri) : this()
    {
        Url = uri.ToString();
        Label = DefaultLabel;
    }

    [PrimaryKey] [AutoIncrement]
    public int Id { get; set; } = -1;

    [Ignore]
    public ICommand HandleReordering
    {
        get => _handleReordering;
        set => SetField(ref _handleReordering, value);
    }

    [Ignore]
    public ICommand GoBack
    {
        get => _goBack;
        set => SetField(ref _goBack, value);
    }

    [Ignore]
    public ICommand Refresh
    {
        get => _refresh;
        set => SetField(ref _refresh, value);
    }

    [Ignore]
    public ICommand ClearFind
    {
        get => _clearFind;
        set => SetField(ref _clearFind, value);
    }

    [Ignore]
    public ICommand Load
    {
        get => _load;
        set => SetField(ref _load, value);
    }

    [Ignore]
    public ICommand Print
    {
        get => _print;
        set => SetField(ref _print, value);
    }

    [Ignore]
    public ICommand FindNext
    {
        get => _findNext;
        set => SetField(ref _findNext, value);
    }

    [Ignore]
    public string Html
    {
        get => _html;
        set => SetField(ref _html, value);
    }

    [Ignore]
    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetField(ref _isRefreshing, value);
    }

    [Ignore]
    public bool CanShowHostCertificate
    {
        get => _canShowHostCertificate;
        set => SetField(ref _canShowHostCertificate, value);
    }

    [Ignore]
    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    [Ignore]
    public bool CanPrint
    {
        get => _canPrint;
        set => SetField(ref _canPrint, value);
    }

    [Ignore]
    public Uri Location
    {
        get => _location;
        set
        {
            SetField(ref _location, value);

            if (Load?.CanExecute(null) ?? false)
            {
                _ = Task.Run(() => { Load.Execute(null); });
            }
        }
    }

    [Ignore]
    public string FindNextQuery
    {
        get => _findNextQuery;
        set
        {
            if (SetField(ref _findNextQuery, value))
            {
                OnPropertyChanged(nameof(HasFindNextQuery));
            }
        }
    }

    [Ignore]
    public bool HasFindNextQuery
    {
        get => !string.IsNullOrEmpty(FindNextQuery);
    }

    [Ignore]
    public string Input
    {
        get => _input;
        set => SetField(ref _input, value);
    }

    [Ignore]
    public string RenderUrl
    {
        get => _renderUrl;
        set => SetField(ref _renderUrl, value);
    }

    [Ignore]
    public ContentPage ParentPage { get; set; }

    [Ignore]
    public bool InitializedByTabCollection { get; set; }

    [Ignore]
    public string DefaultLabel
    {
        get
        {
            var titleMatch = DefaultLabelPattern().Match(Title);
            if (titleMatch.Success)
                return titleMatch.Value;

            var hostMatch = DefaultLabelPattern().Match(Location.Host);
            if (hostMatch.Success)
                return hostMatch.Value;

            // probably should never hit this since we can't have an empty host
            return string.Empty;
        }
    }

    [Ignore]
    public Stack<Uri> RecentHistory
    {
        get => _recentHistory;
        set => SetField(ref _recentHistory, value);
    }

    public string Url { get; set; }

    public string Label
    {
        get => _label;
        set => SetField(ref _label, value);
    }

    public int Order { get; set; }

    public bool Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler<UrlEventArgs> OpeningUrlInNewTab;
    public event EventHandler BookmarkChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public void SetLocationWithoutLoading(Uri location)
    {
        _location = location;
    }

    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return Id.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        return obj is Tab otherTab && otherTab.Id.Equals(Id);
    }

    public override string ToString()
    {
        return $@"{Id} : {Url} : {Label}";
    }

    public virtual void OnOpeningUrlInNewTab(UrlEventArgs e)
    {
        OpeningUrlInNewTab?.Invoke(this, e);
    }

    public virtual void OnBookmarkChanged()
    {
        BookmarkChanged?.Invoke(this, EventArgs.Empty);
    }
}