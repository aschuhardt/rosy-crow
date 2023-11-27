using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CommunityToolkit.Maui.Alerts;
using Microsoft.Extensions.Logging;
using Opal.Authentication;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Resources.Localization;
using RosyCrow.Services.Fingerprint;
using RosyCrow.Services.Fingerprint.Abstractions;

namespace RosyCrow.Services.Identity;

internal class IdentityService : IIdentityService
{
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly IFingerprint _fingerprint;
    private readonly ILogger<IdentityService> _logger;
    private readonly ISettingsDatabase _settingsDatabase;
    private bool _unlockingIdentity = false;

    public IdentityService(IFingerprint fingerprint, ISettingsDatabase settingsDatabase,
        IBrowsingDatabase browsingDatabase, ILogger<IdentityService> logger)
    {
        _fingerprint = fingerprint;
        _settingsDatabase = settingsDatabase;
        _browsingDatabase = browsingDatabase;
        _logger = logger;
    }

    public X509Certificate2 ActiveCertificate { get; private set; }

    public bool ShouldReloadActiveCertificate
    {
        get => _settingsDatabase.ActiveIdentityId.HasValue && ActiveCertificate == null;
    }

    public async Task<X509Certificate2> LoadActiveCertificate()
    {
        try
        {
            // if there is no active cert ID set, then we are done
            if (!_settingsDatabase.ActiveIdentityId.HasValue)
                return null;

            _logger.LogDebug(@"Loading the certificate for the active identity {ID}",
                _settingsDatabase.ActiveIdentityId.Value);

            var identity =
                _browsingDatabase.Identities.FirstOrDefault(i => i.Id == _settingsDatabase.ActiveIdentityId.Value);

            // if no identity with the specified ID exists, clear that ID
            if (identity == null)
            {
                _logger.LogInformation(@"Tried to load the active identity, but it has been deleted");
                ClearActiveCertificate();
                return null;
            }

            ActiveCertificate = await LoadCertificate(identity);

            if (ActiveCertificate == null)
            {
                _logger.LogWarning(@"Failed to load the certificate for the active identity {ID} ({Name})",
                    identity.Id,
                    identity.Name);
                ClearActiveCertificate();
            }

            _logger.LogInformation(@"Loaded the active identity's certificate");

            return ActiveCertificate;
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while loading the active identity's client certificate");
            return null;
        }
    }

    public async Task<X509Certificate2> LoadCertificate(Models.Identity identity)
    {
        try
        {
            var password = await DerivePassword(identity);

            if (password == null)
            {
                _logger.LogInformation(
                    @"Could not derive password for the certificate belonging to identity {ID} ({Name})",
                    identity.Id,
                    identity.Name);
                return null;
            }

            var certificate =
                X509Certificate2.CreateFromEncryptedPemFile(identity.CertificatePath,
                    Encoding.UTF8.GetString(password).ToCharArray());

            _logger.LogInformation(@"Successfully decrypted the certificate for identity {ID} ({Name})",
                identity.Id,
                identity.Name);

            return certificate;
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                @"Exception thrown while loading the certificate for identity {ID} ({Name})",
                identity.Id,
                identity.Name);
            return null;
        }
    }

    public void ClearActiveCertificate()
    {
        _logger.LogDebug(@"Clearing the active identity ID");

        ActiveCertificate = null;
        _settingsDatabase.ActiveIdentityId = null;
    }

    public async Task Activate(Models.Identity identity)
    {
        try
        {
            // don't attempt to load the identity we're already doing so
            if (_unlockingIdentity)
                return;

            // the identity has already been loaded
            if (ActiveCertificate != null)
                return;

            _unlockingIdentity = true;
            _settingsDatabase.ActiveIdentityId = identity.Id;
            ActiveCertificate = await LoadActiveCertificate();
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while activating identity {ID} ({Name})", identity.Id, identity.Name);
        }
        finally
        {
            _unlockingIdentity = false;
        }
    }

    public async Task<Models.Identity> ImportIdentityCertificate(string name, X509Certificate2 certificate,
        Func<Task<bool>> useDeviceCredentialsPrompt)
    {
        var identity = new Models.Identity
        {
            Name = name,
            Hash = certificate.Thumbprint
        };

        identity.SemanticKey = $@"{identity.SanitizedName}_{Guid.NewGuid():N}";

        _logger.LogDebug(@"Generating a new identity named {Name}", identity.Name);
        _logger.LogDebug(@"The new identity's semantic key is {SemanticKey}", identity.SemanticKey);

        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(28) &&
                await CrossFingerprint.Current.IsAvailableAsync(true) &&
                await useDeviceCredentialsPrompt())
            {
                var password = RandomNumberGenerator.GetBytes(32);

                var authConfig = new AuthenticationRequestConfiguration(
                    Text.IdentityPage_GenerateNewKey_Secure_the_Identity,
                    Text.IdentityPage_GenerateNewKey_Authenticate,
                    identity.SemanticKey)
                {
                    AllowAlternativeAuthentication = true
                };

                var result = await _fingerprint.EncryptAsync(authConfig, password);

                if (!result.AuthenticationResult.Authenticated)
                {
                    await Toast.Make(Text.IdentityPage_GenerateNewKey_Could_not_protect_the_identity).Show();
                    return null;
                }

                identity.EncryptedPassword = Convert.ToBase64String(result.Ciphertext);
                identity.EncryptedPasswordIv = Convert.ToBase64String(result.Iv);

                _logger.LogInformation(@"Encrypted the password for the new identity using device credentials");

                await StoreCertificate(identity, certificate, password);
            }
            else
            {
                _logger.LogInformation(@"The new identity's certificate will not use an encrypted password");

                await StoreCertificate(identity, certificate);
            }

            _browsingDatabase.Identities.Add(identity);

            return identity;
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while generating a new identity");
            return null;
        }
    }

    public Task<Models.Identity> GenerateNewIdentity(string name, Func<Task<bool>> useDeviceCredentialsPrompt)
    {
        var certificate = CertificateHelper.GenerateNew(TimeSpan.FromDays(30 * 365), name);
        return ImportIdentityCertificate(name, certificate, useDeviceCredentialsPrompt);
    }

    public async Task<byte[]> DerivePassword(Models.Identity identity)
    {
        try
        {
            _logger.LogDebug(@"Attempting to derive the password for the certificate belonging to identity {ID}",
                identity.Id);

            byte[] password;

            if (identity.EncryptedPassword != null)
            {
                var authConfig = new AuthenticationRequestConfiguration(Text.IdentityService_DerivePassword_Unlock_the_Identity,
                    Text.IdentityService_DerivePassword_Unlock_the_identity_using_your_device_s_credential_,
                    identity.SemanticKey)
                {
                    AllowAlternativeAuthentication = true
                };

                var result =
                    await _fingerprint.DecryptAsync(authConfig,
                        Convert.FromBase64String(identity.EncryptedPassword),
                        Convert.FromBase64String(identity.EncryptedPasswordIv));

                if (!result.AuthenticationResult.Authenticated)
                {
                    _logger.LogInformation(
                        @"Failed to decrypt the identity's password with device credentials (the user likely cancelled the operation)");
                    await Toast.Make(Text.IdentityService_DerivePassword_Could_not_unlock_the_identity).Show();
                    return null;
                }

                _logger.LogInformation(@"Successfully decrypted the password for identity {ID} ({Name})",
                    identity.Id,
                    identity.Name);

                password = result.Plaintext;
            }
            else
            {
                _logger.LogInformation(@"Identity {ID} ({Name}) is not protected by device credentials",
                    identity.Id,
                    identity.Name);
                password = Encoding.UTF8.GetBytes(identity.SemanticKey);
            }

            return password;
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while deriving a certificate's password");
            return null;
        }
    }

    private async Task StoreCertificate(Models.Identity identity, X509Certificate2 certificate, byte[] password = null)
    {
        try
        {
            password ??= await DerivePassword(identity);
            if (password == null)
                return;

            await using var file = File.Create(identity.CertificatePath);
            await using var writer = new StreamWriter(file);
            await certificate.WriteCertificate(writer, password);

            _logger.LogInformation(@"Stored an encrypted PEM-encoded identity certificate");
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                @"Exception thrown while storing the certificate for an identity ({Name})",
                identity.Name);
        }
    }
}