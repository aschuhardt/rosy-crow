using System.Windows.Input;
using CommunityToolkit.Maui.Alerts;
using RosyCrow.Interfaces;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class CertificatePage : ContentPage
{
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly MainPage _mainPage;
    private ICommand _copyText;
    private DateTime _expiration;
    private string _fingerprint;

    private string _host;
    private string _issuer;
    private string _subject;

    public CertificatePage(MainPage mainPage, IBrowsingDatabase browsingDatabase)
    {
        _mainPage = mainPage;
        _browsingDatabase = browsingDatabase;

        InitializeComponent();

        BindingContext = this;

        CopyText = new Command(async value =>
        {
            await Clipboard.SetTextAsync((string)value);
            await Toast.Make("Copied").Show();
        });
    }

    public string Host
    {
        get => _host;
        set
        {
            if (value == _host) return;
            _host = value;
            OnPropertyChanged();
        }
    }

    public string Subject
    {
        get => _subject;
        set
        {
            if (value == _subject) return;
            _subject = value;
            OnPropertyChanged();
        }
    }

    public string Fingerprint
    {
        get => _fingerprint;
        set
        {
            if (value == _fingerprint) return;
            _fingerprint = value;
            OnPropertyChanged();
        }
    }

    public string Issuer
    {
        get => _issuer;
        set
        {
            if (value == _issuer) return;
            _issuer = value;
            OnPropertyChanged();
        }
    }

    public DateTime Expiration
    {
        get => _expiration;
        set
        {
            if (value.Equals(_expiration)) return;
            _expiration = value;
            OnPropertyChanged();
        }
    }

    public ICommand CopyText
    {
        get => _copyText;
        set
        {
            if (Equals(value, _copyText)) return;
            _copyText = value;
            OnPropertyChanged();
        }
    }

    private static string ConvertFingerprintToFriendlyFormat(string fingerprint)
    {
        var buffer = Convert.FromHexString(fingerprint);
        return BitConverter.ToString(buffer).Replace('-', ' ');
    }

    private async void CertificatePage_OnAppearing(object sender, EventArgs e)
    {
        Host = _mainPage.Browser.Location.Host;
        if (_browsingDatabase.TryGetHostCertificate(Host, out var cert))
        {
            Subject = cert.Subject;
            Host = cert.Host;
            Expiration = cert.Expiration;
            Issuer = cert.Issuer;
            Fingerprint = ConvertFingerprintToFriendlyFormat(cert.Fingerprint);
        }
        else
            await Navigation.PopAsync();
    }
}