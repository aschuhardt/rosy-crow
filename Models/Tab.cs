using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RosyCrow.Models;

public class Tab : INotifyPropertyChanged
{
    private bool _selected;

    public Tab(string url, char label)
    {
        Url = url;
        Label = label;
        Id = Guid.NewGuid();
    }

    public string Url { get; set; }
    public char Label { get; set; }
    public Guid Id { get; }
    public int Order { get; set; }

    public bool Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

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
}