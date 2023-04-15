using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Android.Systems;
using CommunityToolkit.Maui.Alerts;
using LiteDB;
using Opal.Authentication;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Services.Fingerprint;
using RosyCrow.Services.Fingerprint.Abstractions;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class IdentityPage : ContentPage
{
    private const int EncryptionIterations = 100;
    private static readonly Regex KeyReplacePattern = new("\\W", RegexOptions.Compiled);
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly IFingerprint _fingerprint;
    private readonly ILiteDatabase _liteDatabase; // for file storage
    private ICommand _generateNew;
    private ObservableCollection<Identity> _identities;

    public IdentityPage(IBrowsingDatabase browsingDatabase, IFingerprint fingerprint, ILiteDatabase liteDatabase)
    {
        InitializeComponent();

        _browsingDatabase = browsingDatabase;
        _fingerprint = fingerprint;
        _liteDatabase = liteDatabase;

        BindingContext = this;

        GenerateNew = new Command(async () => await GenerateNewKey());
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

    private async Task GenerateNewKey()
    {
        var name = await DisplayPromptAsync("Generate Identity", "Enter the name you would like to use for this key.",
            maxLength: 400);
        var key = $"_ident_{KeyReplacePattern.Replace(name, "_").ToLowerInvariant()}_{Guid.NewGuid():N}";

        var certificate = CertificateHelper.GenerateNew(TimeSpan.FromDays(10), name);

        var identity = new Identity
        {
            Name = name,
            SemanticKey = key,
            Hash = certificate.Thumbprint,
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
                Toast.Make("Could not protect the identity");
                return;
            }

            identity.EncryptedPassword = Convert.ToBase64String(result.Ciphertext);

            await StoreCertificate(identity, certificate, password);
        }
        else
        {
            await StoreCertificate(identity, certificate);
        }

        _browsingDatabase.Identities.Add(identity);
    }

    private async Task StoreCertificate(Identity identity, X509Certificate2 certificate, byte[] password = null)
    {
        var directory = Path.Combine(FileSystem.Current.AppDataDirectory, "certificates");
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        foreach (var path in Directory.GetFiles(directory))
            File.Delete(path);

        password ??= await DerivePassword(identity);
        if (password == null)
            return;

        await using var storage = _liteDatabase.FileStorage.OpenWrite(identity.SemanticKey, $"{identity.SemanticKey}.pem");
        await using var writer = new StreamWriter(storage);
        await writer.WriteLineAsync(PemEncoding.Write("CERTIFICATE", certificate.RawData));
        await writer.WriteLineAsync(PemEncoding.Write("ENCRYPTED PRIVATE KEY",
            certificate.GetRSAPrivateKey()?.ExportEncryptedPkcs8PrivateKey(password,
                new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA512, EncryptionIterations))));
        await writer.FlushAsync();
    }

    private async Task<byte[]> DerivePassword(Identity identity)
    {
        byte[] password;
        if (identity.EncryptedPassword != null)
        {
            var authConfig = new AuthenticationRequestConfiguration("Unlock the Identity",
                "Unlock the identity using your device's credential.", identity.SemanticKey)
            {
                AllowAlternativeAuthentication = true
            };

            var result =
                await _fingerprint.DecryptAsync(authConfig, Convert.FromBase64String(identity.EncryptedPassword));
            if (!result.AuthenticationResult.Authenticated)
            {
                Toast.Make("Could not unlock the identity");
                return null;
            }

            password = result.Plaintext;
        }
        else
            password = Encoding.UTF8.GetBytes(identity.SemanticKey);

        return password;
    }

    private async Task<X509Certificate2> LoadCertificate(Identity identity)
    {
        var password = await DerivePassword(identity);
        if (password == null)
            return null;

        var path = Path.GetTempFileName();
        await using (var file = File.OpenWrite(path))
            _liteDatabase.FileStorage.Download(identity.SemanticKey, file);

        return X509Certificate2.CreateFromEncryptedPemFile(path, Encoding.UTF8.GetString(password).ToCharArray());
    }

    private void IdentityPage_OnAppearing(object sender, EventArgs e)
    {
        Identities = _browsingDatabase.Identities;
    }
}