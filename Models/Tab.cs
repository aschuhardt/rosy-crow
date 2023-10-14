using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RosyCrow.Views;
using SQLite;

namespace RosyCrow.Models;

public class Tab : INotifyPropertyChanged
{
    private string _label;
    private bool _selected;

    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    private ICommand _selectedChanged;

    [Ignore]
    public ICommand SelectedChanged
    {
        get => _selectedChanged;
        set => SetField(ref _selectedChanged, value);
    }

    public Tab()
    {
    }

    public Tab(string url, string label)
    {
        Url = url;
        Label = label;
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
        set
        {
            if (SetField(ref _selected, value))
                _selectedChanged?.Execute(this);
        }
    }

    [Ignore]
    public BrowserView View { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;

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

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        return obj is Tab otherTab && otherTab.Id.Equals(Id);
    }

    public override string ToString()
    {
        return $"{Id} : {Url} : {Label}";
    }
}