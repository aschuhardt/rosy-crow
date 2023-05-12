using System.Security.Cryptography.X509Certificates;
using System.Text;
using CommunityToolkit.Maui.Alerts;
using LiteDB;
using Microsoft.Extensions.Logging;
using RosyCrow.Interfaces;
using RosyCrow.Services.Fingerprint.Abstractions;

namespace RosyCrow.Services.Identity;

internal class IdentityService : IIdentityService
{
    private readonly IFingerprint _fingerprint;
    private readonly ISettingsDatabase _settingsDatabase;
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly ILiteDatabase _liteDatabase;
    private readonly ILogger<IdentityService> _logger;

    private X509Certificate2 _activeCertificate;

    public IdentityService(IFingerprint fingerprint, ISettingsDatabase settingsDatabase, IBrowsingDatabase browsingDatabase, ILiteDatabase liteDatabase, ILogger<IdentityService> logger)
    {
        _fingerprint = fingerprint;
        _settingsDatabase = settingsDatabase;
        _browsingDatabase = browsingDatabase;
        _liteDatabase = liteDatabase;
        _logger = logger;
    }

    public X509Certificate2 ActiveCertificate { get; private set; }

    public bool ShouldReloadActiveCertificate =>
        _settingsDatabase.ActiveIdentityId.HasValue && ActiveCertificate == null;

    public async Task<X509Certificate2> LoadActiveCertificate()
    {
        try
        {
            // if there is no active cert ID set, then we are done
            if (!_settingsDatabase.ActiveIdentityId.HasValue)
                return null;

            _logger.LogDebug("Loading the certificate for the active identity {ID}", _settingsDatabase.ActiveIdentityId.Value);

            var identity =
                _browsingDatabase.Identities.FirstOrDefault(i => i.Id == _settingsDatabase.ActiveIdentityId.Value);

            // if no identity with the specified ID exists, clear that ID
            if (identity == null)
            {
                _logger.LogInformation("Tried to load the active identity, but it has been deleted");
                ClearActiveCertificate();
                return null;
            }

            ActiveCertificate = await LoadCertificate(identity);

            if (ActiveCertificate == null)
            {
                _logger.LogWarning("Failed to load the certificate for the active identity {ID} ({Name})", identity.Id, identity.Name);
                ClearActiveCertificate();
            }

            _logger.LogInformation("Loaded the active identity's certificate");

            return ActiveCertificate;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while loading the active identity's client certificate");
            return null;
        }
    }

    private async Task<X509Certificate2> LoadCertificate(Models.Identity identity)
    {
        try
        {
            var password = await DerivePassword(identity);
            if (password == null)
            {
                _logger.LogInformation("Could not derive password for the certificate belonging to identity {ID} ({Name})", identity.Id, identity.Name);
                return null;
            }

            var path = Path.GetTempFileName();
            _liteDatabase.FileStorage.Download(identity.SemanticKey, path, true);

            var certificate =
                X509Certificate2.CreateFromEncryptedPemFile(path, Encoding.UTF8.GetString(password).ToCharArray());

            File.Delete(path);

            _logger.LogInformation("Successfully decrypted the certificate for identity {ID} ({Name})", identity.Id, identity.Name);

            return certificate;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while loading the certificate for identity {ID} ({Name})", identity.Id, identity.Name);
            return null;
        }
    }

    public async Task<byte[]> DerivePassword(Models.Identity identity)
    {
        try
        {
            _logger.LogDebug("Attempting to derive the password for the certificate belonging to identity {ID}", identity.Id);

            byte[] password;
            if (identity.EncryptedPassword != null)
            {
                var authConfig = new AuthenticationRequestConfiguration("Unlock the Identity",
                    "Unlock the identity using your device's credential.", identity.SemanticKey)
                {
                    AllowAlternativeAuthentication = true
                };

                var result =
                    await _fingerprint.DecryptAsync(authConfig, Convert.FromBase64String(identity.EncryptedPassword), Convert.FromBase64String(identity.EncryptedPasswordIv));
                if (!result.AuthenticationResult.Authenticated)
                {
                    _logger.LogInformation("Failed to decrypt the identity's password with device credentials (the user likely cancelled the operation)");
                    await Toast.Make("Could not unlock the identity").Show();
                    return null;
                }

                _logger.LogInformation("Successfully decrypted the password for identity {ID} ({Name})", identity.Id, identity.Name);

                password = result.Plaintext;
            }
            else
            {
                _logger.LogInformation("Identity {ID} ({Name}) is not protected by device credentials", identity.Id, identity.Name);
                password = Encoding.UTF8.GetBytes(identity.SemanticKey);
            }

            return password;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while deriving a certificate's password");
            return null;
        }
    }

    public void ClearActiveCertificate()
    {
        _logger.LogDebug("Clearing the active identity ID");

        ActiveCertificate = null;
        _settingsDatabase.ActiveIdentityId = null;
    }

    public async Task Activate(Models.Identity identity)
    {
        try
        {
            _activeCertificate = null;
            _settingsDatabase.ActiveIdentityId = identity.Id;
            ActiveCertificate = await LoadActiveCertificate();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while activating identity {ID} ({Name})", identity.Id, identity.Name);
        }
    }
}