using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Android.Media;
using CommunityToolkit.Maui.Alerts;
using LiteDB;
using Opal.Authentication;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Services.Fingerprint;
using RosyCrow.Services.Fingerprint.Abstractions;
using RosyCrow.Services.Fingerprint.Platforms.Android.Utils;
using RosyCrow.Services.Identity;
using Encoding = System.Text.Encoding;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class IdentityPage : ContentPage
{
    private const int EncryptionIterations = 100;
    private static readonly Regex KeyReplacePattern = new("\\W", RegexOptions.Compiled);
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly IFingerprint _fingerprint;
    private readonly IIdentityService _identityService;
    private readonly ILiteDatabase _liteDatabase; // for file storage
    private readonly ISettingsDatabase _settingsDatabase;
    private ICommand _delete;
    private ICommand _generateNew;
    private ObservableCollection<Identity> _identities;
    private ICommand _toggleActive;

    public IdentityPage(IBrowsingDatabase browsingDatabase, IFingerprint fingerprint, ILiteDatabase liteDatabase,
        IIdentityService identityService, ISettingsDatabase settingsDatabase)
    {
        InitializeComponent();

        _browsingDatabase = browsingDatabase;
        _fingerprint = fingerprint;
        _liteDatabase = liteDatabase;
        _identityService = identityService;
        _settingsDatabase = settingsDatabase;

        BindingContext = this;

        GenerateNew = new Command(async () => await GenerateNewKey());
        Delete = new Command(async id => await DeleteKey((int)id));
        ToggleActive = new Command(async id => await ToggleActiveKey((int)id));
    }

    public ObservableCollection<Identity> Identities
    {
        get => _identities;
        set
        {
            if (Equals(value, _identities)) return;
            _identities = value;
            OnPropertyChanged();
        }
    }

    public ICommand GenerateNew
    {
        get => _generateNew;
        set
        {
            if (Equals(value, _generateNew)) return;
            _generateNew = value;
            OnPropertyChanged();
        }
    }

    public ICommand Delete
    {
        get => _delete;
        set
        {
            if (Equals(value, _delete)) return;
            _delete = value;
            OnPropertyChanged();
        }
    }

    public ICommand ToggleActive
    {
        get => _toggleActive;
        set
        {
            if (Equals(value, _toggleActive)) return;
            _toggleActive = value;
            OnPropertyChanged();
        }
    }

    private async Task ToggleActiveKey(int id)
    {
        var identity = Identities.FirstOrDefault(i => i.Id == id);
        if (identity == null)
            return;

        if (id == _settingsDatabase.ActiveIdentityId.GetValueOrDefault(-1))
        {
            ClearIdentityActiveIndicator();
            _identityService.ClearActiveCertificate();
        }
        else
        {
            SetIdentityActiveIndicator(id);
            await _identityService.Activate(identity);
        }
    }

    private void SetIdentityActiveIndicator(int activeId)
    {
        foreach (var identity in Identities)
            identity.IsActive = identity.Id == activeId;
    }

    private void ClearIdentityActiveIndicator()
    {
        foreach (var identity in Identities)
            identity.IsActive = false;
    }

    private async Task DeleteKey(int id)
    {
        var identity = Identities.FirstOrDefault(i => i.Id == id);
        if (identity == null)
            return;

        if (!await DisplayAlert("Delete Identity", "Are you sure you want to delete this identity?", "Yes", "No"))
            return;

        if (string.IsNullOrEmpty(identity.EncryptedPassword) || new CryptoObjectHelper(identity.SemanticKey).Delete())
        {
            Identities.Remove(identity);
            _liteDatabase.FileStorage.Delete(identity.SemanticKey);
            await Toast.Make("Identity deleted").Show();
        }
        else
            await Toast.Make("Failed to delete identity").Show();
    }

    private async Task GenerateNewKey()
    {
        var name = await DisplayPromptAsync("Generate Identity", "Enter the name you would like to use for this key.",
            maxLength: 400);

        if (string.IsNullOrWhiteSpace(name))
            return;

        var key = $"_ident_{KeyReplacePattern.Replace(name, "_").ToLowerInvariant()}_{Guid.NewGuid():N}";

        var certificate = CertificateHelper.GenerateNew(TimeSpan.FromDays(30 * 365), name);

        var identity = new Identity
        {
            Name = name,
            SemanticKey = key,
            Hash = certificate.Thumbprint
        };

        if (OperatingSystem.IsAndroidVersionAtLeast(28) &&
            await CrossFingerprint.Current.IsAvailableAsync(true) &&
            await DisplayAlert("Generate Identity", "Secure this identity with your device credentials?", "Yes", "No"))
        {
            var password = RandomNumberGenerator.GetBytes(32);

            var authConfig = new AuthenticationRequestConfiguration(
                "Secure the Identity",
                "Authenticate to protect the new identity's encryption key.", key)
            {
                AllowAlternativeAuthentication = true
            };

            var result = await _fingerprint.EncryptAsync(authConfig, password);
            if (!result.AuthenticationResult.Authenticated)
            {
                await Toast.Make("Could not protect the identity").Show();
                return;
            }

            identity.EncryptedPassword = Convert.ToBase64String(result.Ciphertext);
            identity.EncryptedPasswordIv = Convert.ToBase64String(result.Iv);

            await StoreCertificate(identity, certificate, password);
        }
        else
            await StoreCertificate(identity, certificate);

        Identities.Add(identity);
    }

    private async Task StoreCertificate(Identity identity, X509Certificate2 certificate, byte[] password = null)
    {
        password ??= await _identityService.DerivePassword(identity);
        if (password == null)
            return;

        await using var storage =
            _liteDatabase.FileStorage.OpenWrite(identity.SemanticKey, $"{identity.SemanticKey}.pem");
        await using var writer = new StreamWriter(storage);
        await writer.WriteLineAsync(PemEncoding.Write("CERTIFICATE", certificate.RawData));
        await writer.WriteLineAsync(PemEncoding.Write("ENCRYPTED PRIVATE KEY",
            certificate.GetRSAPrivateKey()?.ExportEncryptedPkcs8PrivateKey(Encoding.UTF8.GetString(password).ToCharArray(),
                new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA512, EncryptionIterations))));
        await writer.FlushAsync();
    }

    private void IdentityPage_OnAppearing(object sender, EventArgs e)
    {
        Identities = _browsingDatabase.Identities;
        if (_settingsDatabase.ActiveIdentityId.HasValue)
            SetIdentityActiveIndicator(_settingsDatabase.ActiveIdentityId.Value);
        else
            ClearIdentityActiveIndicator();
    }
}