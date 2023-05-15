using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using CommunityToolkit.Maui.Alerts;
using Microsoft.Extensions.Logging;
using Opal.Authentication;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Resources.Localization;
using RosyCrow.Services.Fingerprint;
using RosyCrow.Services.Fingerprint.Abstractions;
using RosyCrow.Services.Fingerprint.Platforms.Android.Utils;
using RosyCrow.Services.Identity;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class IdentityPage : ContentPage
{
    private const int EncryptionIterations = 100;
    private static readonly Regex KeyReplacePattern = new("\\W", RegexOptions.Compiled);
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly IFingerprint _fingerprint;
    private readonly IIdentityService _identityService;
    private readonly ISettingsDatabase _settingsDatabase;
    private readonly ILogger<IdentityPage> _logger;

    private ICommand _delete;
    private ICommand _generateNew;
    private ObservableCollection<Identity> _identities;
    private ICommand _toggleActive;

    public IdentityPage(IBrowsingDatabase browsingDatabase, IFingerprint fingerprint,
        IIdentityService identityService, ISettingsDatabase settingsDatabase, ILogger<IdentityPage> logger)
    {
        _browsingDatabase = browsingDatabase;
        _fingerprint = fingerprint;
        _identityService = identityService;
        _settingsDatabase = settingsDatabase;
        _logger = logger;

        InitializeComponent();

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
        try
        {
            var identity = Identities.FirstOrDefault(i => i.Id == id);
            if (identity == null)
                return;

            if (id == _settingsDatabase.ActiveIdentityId.GetValueOrDefault(-1))
            {
                ClearIdentityActiveIndicator();
                _identityService.ClearActiveCertificate();
                _logger.LogInformation("Deactivated identity {ID} ({Name})", identity.Id, identity.Name);
            }
            else
            {
                SetIdentityActiveIndicator(id);
                await _identityService.Activate(identity);
                _logger.LogInformation("Activated identity {ID} ({Name})", identity.Id, identity.Name);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while toggling the active status of identity {ID}", id);
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
        try
        {
            var identity = Identities.FirstOrDefault(i => i.Id == id);
            if (identity == null)
                return;

            if (!await DisplayAlert(Text.IdentityPage_DeleteKey_Delete_Identity, Text.IdentityPage_DeleteKey_Confirm,
                    Text.Global_Yes, Text.Global_No))
                return;

            if (string.IsNullOrEmpty(identity.EncryptedPassword) || new CryptoObjectHelper(identity.SemanticKey).Delete())
            {
                Identities.Remove(identity);
                if (File.Exists(identity.CertificatePath))
                    File.Delete(identity.CertificatePath);
                await Toast.Make(Text.IdentityPage_DeleteKey_Identity_deleted).Show();
            }
            else
                await Toast.Make(Text.IdentityPage_DeleteKey_Failed_to_delete_identity).Show();

            _logger.LogInformation("Deleted identity {ID} ({Name})", identity.Id, identity.Name);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while deleting identity {ID}", id);
        }
    }

    private async Task GenerateNewKey()
    {
        var name = await DisplayPromptAsync(Text.IdentityPage_GenerateNewKey_Generate_Identity,
            Text.IdentityPage_GenerateNewKey_Prompt,
            maxLength: 400);

        if (string.IsNullOrWhiteSpace(name))
            return;

        var key = $"{KeyReplacePattern.Replace(name, "_").ToLowerInvariant()}_{Guid.NewGuid():N}";

        var certificate = CertificateHelper.GenerateNew(TimeSpan.FromDays(30 * 365), name);

        var identity = new Identity
        {
            Name = name,
            SemanticKey = key,
            Hash = certificate.Thumbprint
        };

        _logger.LogDebug("Generating a new identity named {Name}", identity.Name);
        _logger.LogDebug("The new identity's semantic key is {SemanticKey}", key);

        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(28) &&
                await CrossFingerprint.Current.IsAvailableAsync(true) &&
                await DisplayAlert(Text.IdentityPage_GenerateNewKey_Generate_Identity,
                    Text.IdentityPage_GenerateNewKey_Secure, Text.Global_Yes, Text.Global_No))
            {
                var password = RandomNumberGenerator.GetBytes(32);

                var authConfig = new AuthenticationRequestConfiguration(
                    Text.IdentityPage_GenerateNewKey_Secure_the_Identity,
                    Text.IdentityPage_GenerateNewKey_Authenticate, key)
                {
                    AllowAlternativeAuthentication = true
                };

                var result = await _fingerprint.EncryptAsync(authConfig, password);
                if (!result.AuthenticationResult.Authenticated)
                {
                    await Toast.Make(Text.IdentityPage_GenerateNewKey_Could_not_protect_the_identity).Show();
                    return;
                }

                identity.EncryptedPassword = Convert.ToBase64String(result.Ciphertext);
                identity.EncryptedPasswordIv = Convert.ToBase64String(result.Iv);

                _logger.LogInformation("Encrypted the password for the new identity using device credentials");

                await StoreCertificate(identity, certificate, password);
            }
            else
            {
                _logger.LogInformation("The new identity's certificate will not use an encrypted password");

                await StoreCertificate(identity, certificate);
            }

            Identities.Add(identity);

            _logger.LogInformation("Saved the new identity named {Name}", identity.Name);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while generating a new identity");
        }
    }

    private async Task StoreCertificate(Identity identity, X509Certificate2 certificate, byte[] password = null)
    {
        try
        {
            password ??= await _identityService.DerivePassword(identity);
            if (password == null)
                return;

            await using var file = File.Create(identity.CertificatePath);
            await using var writer = new StreamWriter(file);
            await writer.WriteLineAsync(PemEncoding.Write("CERTIFICATE", certificate.RawData));
            await writer.WriteLineAsync(PemEncoding.Write("ENCRYPTED PRIVATE KEY",
                certificate.GetRSAPrivateKey()?.ExportEncryptedPkcs8PrivateKey(
                    Encoding.UTF8.GetString(password).ToCharArray(),
                    new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA512, EncryptionIterations))));
            await writer.FlushAsync();

            _logger.LogInformation("Stored an encrypted PEM-encoded identity certificate");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while storing the certificate for an identity ({Name})", identity.Name);
        }
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