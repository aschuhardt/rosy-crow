using System.ComponentModel;
using System.Runtime.CompilerServices;
using SQLite;

namespace RosyCrow.Models;

public class Identity : INotifyPropertyChanged
{
    private bool _isActive;

    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Name { get; set; }
    public string SemanticKey { get; set; }
    public string Hash { get; set; }
    public string EncryptedPassword { get; set; }
    public string EncryptedPasswordIv { get; set; }

    [Ignore]
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (value == _isActive) return;
            _isActive = value;
            OnPropertyChanged();
        }
    }

    [Ignore]
    public string CertificatePath =>
        Path.Combine(FileSystem.AppDataDirectory, Constants.CertificateDirectory, $"{SemanticKey}.pem");

    public event PropertyChangedEventHandler PropertyChanged;

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