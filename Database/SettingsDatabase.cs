using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiteDB;
using Yarrow.Interfaces;
using Yarrow.Models;

namespace Yarrow.Database;

internal class SettingsDatabase : ISettingsDatabase, INotifyPropertyChanged
{
    private readonly ILiteCollection<Setting> _settingsStore;
    private string _homeUrl;
    private string _lastVisitedUrl;

    public SettingsDatabase(ILiteDatabase database)
    {
        _settingsStore = database.GetCollection<Setting>();
        _settingsStore.EnsureIndex(s => s.Name, true);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string HomeUrl
    {
        get => _homeUrl ?? GetStringValue();
        set
        {
            if (SetField(ref _homeUrl, value))
                SetStringValue(value);
        }
    }

    public string LastVisitedUrl
    {
        get => _lastVisitedUrl ?? GetStringValue();
        set
        {
            if (SetField(ref _lastVisitedUrl, value))
                SetStringValue(value);
        }
    }

    private void SetStringValue(string value, [CallerMemberName] string name = null)
    {
        if (name == null)
            return;

        var entity = _settingsStore.FindOne(s => s.Name.Equals(name)) ?? new Setting { Name = name };
        entity.StringValue = value;

        _settingsStore.Upsert(entity);
    }

    private void SetIntValue(int value, [CallerMemberName] string name = null)
    {
        if (name == null)
            return;

        var entity = _settingsStore.FindOne(s => s.Name.Equals(name)) ?? new Setting { Name = name };
        entity.IntValue = value;

        _settingsStore.Upsert(entity);
    }

    private string GetStringValue(string defaultValue = null, [CallerMemberName] string name = null)
    {
        if (name == null)
            return null;

        var entity = _settingsStore.FindOne(s => s.Name.Equals(name));

        if (entity != null)
            return entity.StringValue;

        entity = new Setting { Name = name, StringValue = defaultValue };
        _settingsStore.Insert(entity);

        return entity.StringValue;
    }

    private int GetIntValue(int defaultValue = default, [CallerMemberName] string name = null)
    {
        if (name == null)
            return default;

        var entity = _settingsStore.FindOne(s => s.Name.Equals(name));

        if (entity != null)
            return entity.IntValue;

        entity = new Setting { Name = name, IntValue = defaultValue };
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