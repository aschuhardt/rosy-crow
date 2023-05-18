using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using SQLite;

namespace RosyCrow.Database;

internal class SettingsDatabase : ISettingsDatabase, INotifyPropertyChanged
{
    private readonly SQLiteConnection _database;
    private readonly ILogger<SettingsDatabase> _logger;


    private int? _activeIdentityId;
    private string _homeUrl;
    private string _lastVisitedUrl;
    private bool? _storeVisited;
    private string _theme;
    private int? _historyPageSize;
    private bool? _inlineImages;
    private bool? _strictTofuMode;

    public SettingsDatabase(ILogger<SettingsDatabase> logger, SQLiteConnection database)
    {
        _logger = logger;
        _database = database;

        _database.CreateTable<Setting>();
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
            _inlineImages ??= GetBoolValue(false);
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
            _strictTofuMode ??= GetBoolValue(false);
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
            _storeVisited ??= GetBoolValue(false);
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

        try
        {
            var entity = _database.Table<Setting>().FirstOrDefault(s => s.Name == name);

            if (entity != null && !value.HasValue)
                _database.Delete(entity);
            else if (value.HasValue)
            {
                _logger.LogInformation("Setting {Name} to {Value}", name, value.GetValueOrDefault());

                entity ??= new Setting { Name = name };
                entity.BoolValue = value.Value;
                _database.InsertOrReplace(entity);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while setting the value of {Name}", name);
        }
    }

    private void SetStringValue(string value, [CallerMemberName] string name = null)
    {
        if (name == null)
            return;

        try
        {
            var entity = _database.Table<Setting>().FirstOrDefault(s => s.Name == name);

            if (entity != null && string.IsNullOrEmpty(value))
                _database.Delete(entity);
            else if (!string.IsNullOrEmpty(value))
            {
                _logger.LogInformation("Setting {Name} to \"{Value}\"", name, value);

                entity ??= new Setting { Name = name };
                entity.StringValue = value;
                _database.InsertOrReplace(entity);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while setting the value of {Name}", name);
        }
    }

    private void SetIntValue(int? value, [CallerMemberName] string name = null)
    {
        if (name == null)
            return;

        try
        {
            var entity = _database.Table<Setting>().FirstOrDefault(s => s.Name == name);

            if (entity != null && !value.HasValue)
                _database.Delete(entity);
            else if (value.HasValue)
            {
                _logger.LogInformation("Setting {Name} to {Value}", name, value.GetValueOrDefault());

                entity ??= new Setting { Name = name };
                entity.IntValue = value.Value;
                _database.InsertOrReplace(entity);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while setting the value of {Name}", name);
        }
    }

    private string GetStringValue(string defaultValue = null, [CallerMemberName] string name = null)
    {
        if (name == null)
            return defaultValue;

        try
        {
            var entity = _database.Table<Setting>().FirstOrDefault(s => s.Name == name);

            if (entity != null)
                return entity.StringValue;

            if (string.IsNullOrEmpty(defaultValue))
                return null;

            entity = new Setting { Name = name, StringValue = defaultValue };
            _database.Insert(entity);

            return entity.StringValue;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while reading the value of {Name}", name);
            return defaultValue;
        }
    }

    private bool? GetBoolValue(bool? defaultValue = null, [CallerMemberName] string name = null)
    {
        if (name == null)
            return defaultValue;

        try
        {
            var entity = _database.Table<Setting>().FirstOrDefault(s => s.Name == name);

            if (entity != null)
                return entity.BoolValue;

            if (!defaultValue.HasValue)
                return null;

            entity = new Setting { Name = name, BoolValue = defaultValue.Value };
            _database.Insert(entity);

            return entity.BoolValue;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while reading the value of {Name}", name);
            return defaultValue;
        }
    }

    private int? GetIntValue(int? defaultValue = null, [CallerMemberName] string name = null)
    {
        if (name == null)
            return defaultValue;

        try
        {
            var entity = _database.Table<Setting>().FirstOrDefault(s => s.Name == name);

            if (entity != null)
                return entity.IntValue;

            if (!defaultValue.HasValue)
                return null;

            entity = new Setting { Name = name, IntValue = defaultValue.Value };
            _database.Insert(entity);

            return entity.IntValue;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while reading the value of {Name}", name);
            return defaultValue;
        }
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