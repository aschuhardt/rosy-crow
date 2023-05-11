using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiteDB;
using RosyCrow.Interfaces;
using RosyCrow.Models;

namespace RosyCrow.Database;

internal class SettingsDatabase : ISettingsDatabase, INotifyPropertyChanged
{
    private readonly ILiteCollection<Setting> _settingsStore;
    private int? _activeIdentityId;
    private string _homeUrl;
    private string _lastVisitedUrl;
    private bool? _storeVisited;
    private string _theme;
    private int? _historyPageSize;
    private bool? _inlineImages;
    private bool? _strictTofuMode;

    public SettingsDatabase(ILiteDatabase database)
    {
        _settingsStore = database.GetCollection<Setting>();
        _settingsStore.EnsureIndex(s => s.Name, true);
    }

    public int? ActiveIdentityId
    {
        get
        {
            _activeIdentityId ??= GetIntValue();
            return _activeIdentityId;
        }
        set
        {
            if (SetField(ref _activeIdentityId, value))
                SetIntValue(value);
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string Theme
    {
        get
        {
            _theme ??= GetStringValue("sakura-dark");
            return _theme;
        }
        set
        {
            if (SetField(ref _theme, value))
                SetStringValue(value);
        }
    }

    public string HomeUrl
    {
        get
        {
            _homeUrl ??= GetStringValue();
            return _homeUrl;
        }
        set
        {
            if (SetField(ref _homeUrl, value))
                SetStringValue(value);
        }
    }

    public int HistoryPageSize
    {
        get
        {
            _historyPageSize ??= GetIntValue(16);
            return _historyPageSize.GetValueOrDefault();
        }
        set
        {
            if (SetField(ref _historyPageSize, value))
                SetIntValue(value);
        }
    }

    public string LastVisitedUrl
    {
        get
        {
            _lastVisitedUrl ??= GetStringValue();
            return _lastVisitedUrl;
        }
        set
        {
            if (SetField(ref _lastVisitedUrl, value))
                SetStringValue(value);
        }
    }

    public bool InlineImages
    {
        get
        {
            _inlineImages ??= GetBoolValue();
            return _inlineImages.GetValueOrDefault();
        }
        set
        {
            if (SetField(ref _inlineImages, value))
                SetBoolValue(value);
        }
    }

    public bool StrictTofuMode
    {
        get
        {
            _strictTofuMode ??= GetBoolValue();
            return _strictTofuMode.GetValueOrDefault();
        }
        set
        {
            if (SetField(ref _strictTofuMode, value))
                SetBoolValue(value);
        }
    }

    public bool SaveVisited
    {
        get
        {
            _storeVisited ??= GetBoolValue();
            return _storeVisited.GetValueOrDefault();
        }
        set
        {
            if (SetField(ref _storeVisited, value))
                SetBoolValue(value);
        }
    }

    private void SetBoolValue(bool? value, [CallerMemberName] string name = null)
    {
        if (name == null)
            return;

        var entity = _settingsStore.FindOne(s => s.Name.Equals(name));

        if (entity != null && !value.HasValue)
            _settingsStore.Delete(entity.Id);
        else if (value.HasValue)
        {
            entity ??= new Setting { Name = name };
            entity.BoolValue = value.Value;
            _settingsStore.Upsert(entity);
        }
    }

    private void SetStringValue(string value, [CallerMemberName] string name = null)
    {
        if (name == null)
            return;

        var entity = _settingsStore.FindOne(s => s.Name.Equals(name));

        if (entity != null && string.IsNullOrEmpty(value))
            _settingsStore.Delete(entity.Id);
        else if (!string.IsNullOrEmpty(value))
        {
            entity ??= new Setting { Name = name };
            entity.StringValue = value;
            _settingsStore.Upsert(entity);
        }

        _settingsStore.Upsert(entity);
    }

    private void SetIntValue(int? value, [CallerMemberName] string name = null)
    {
        if (name == null)
            return;

        var entity = _settingsStore.FindOne(s => s.Name.Equals(name));

        if (entity != null && !value.HasValue)
            _settingsStore.Delete(entity.Id);
        else if (value.HasValue)
        {
            entity ??= new Setting { Name = name };
            entity.IntValue = value.Value;
            _settingsStore.Upsert(entity);
        }
    }

    private string GetStringValue(string defaultValue = null, [CallerMemberName] string name = null)
    {
        if (name == null)
            return null;

        var entity = _settingsStore.FindOne(s => s.Name.Equals(name));

        if (entity != null)
            return entity.StringValue;

        if (string.IsNullOrEmpty(defaultValue))
            return null;

        entity = new Setting { Name = name, StringValue = defaultValue };
        _settingsStore.Insert(entity);

        return entity.StringValue;
    }

    private bool? GetBoolValue(bool? defaultValue = null, [CallerMemberName] string name = null)
    {
        if (name == null)
            return default;

        var entity = _settingsStore.FindOne(s => s.Name.Equals(name));

        if (entity != null)
            return entity.BoolValue;

        if (!defaultValue.HasValue)
            return null;

        entity = new Setting { Name = name, BoolValue = defaultValue.Value };
        _settingsStore.Insert(entity);

        return entity.BoolValue;
    }

    private int? GetIntValue(int? defaultValue = null, [CallerMemberName] string name = null)
    {
        if (name == null)
            return default;

        var entity = _settingsStore.FindOne(s => s.Name.Equals(name));

        if (entity != null)
            return entity.IntValue;

        if (!defaultValue.HasValue)
            return null;

        entity = new Setting { Name = name, IntValue = defaultValue.Value };
        _settingsStore.Insert(entity);

        return entity.IntValue;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}