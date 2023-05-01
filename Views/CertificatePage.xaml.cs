using RosyCrow.Interfaces;
using RosyCrow.Models;

#pragma warning disable CS1998

namespace RosyCrow.Views;

public partial class CertificatePage : ContentPage
{
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly MainPage _mainPage;

    private HostCertificate _certificate;
    private string _host1;

    public CertificatePage(MainPage mainPage, IBrowsingDatabase browsingDatabase)
    {
        _mainPage = mainPage;
        _browsingDatabase = browsingDatabase;

        InitializeComponent();
    }

    public HostCertificate Certificate
    {
        get => _certificate;
        set
        {
            if (Equals(value, _certificate)) return;
            _certificate = value;
            OnPropertyChanged();
        }
    }

    public string Host
    {
        get => _host1;
        set
        {
            if (value == _host1) return;
            _host1 = value;
            OnPropertyChanged();
        }
    }

    private async void CertificatePage_OnAppearing(object sender, EventArgs e)
    {
        Host = _mainPage.Browser.Location.Host;
        if (_browsingDatabase.TryGetHostCertificate(Host, out var cert))
            Certificate = cert;
        else
            Navigation.PopAsync();
    }
}