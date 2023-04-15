using System.Security.Cryptography.X509Certificates;
using System.Text;
using CommunityToolkit.Maui.Alerts;
using LiteDB;
using RosyCrow.Interfaces;
using RosyCrow.Services.Fingerprint.Abstractions;

namespace RosyCrow.Services.Identity;

internal class IdentityService : IIdentityService
{
    private readonly IFingerprint _fingerprint;
    private readonly ISettingsDatabase _settingsDatabase;
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly ILiteDatabase _liteDatabase;

    private X509Certificate2 _activeCertificate;

    public IdentityService(IFingerprint fingerprint, ISettingsDatabase settingsDatabase, IBrowsingDatabase browsingDatabase, ILiteDatabase liteDatabase)
    {
        _fingerprint = fingerprint;
        _settingsDatabase = settingsDatabase;
        _browsingDatabase = browsingDatabase;
        _liteDatabase = liteDatabase;
    }

    public X509Certificate2 ActiveCertificate { get; private set; }

    public bool ShouldReloadActiveCertificate =>
        _settingsDatabase.ActiveIdentityId.HasValue && ActiveCertificate == null;

    public async Task<X509Certificate2> LoadActiveCertificate()
    {
        // if there is no active cert ID set, then we are done
        if (!_settingsDatabase.ActiveIdentityId.HasValue)
            return null;

        var identity =
            _browsingDatabase.Identities.FirstOrDefault(i => i.Id == _settingsDatabase.ActiveIdentityId.Value);

        // if no identity with the specified ID exists, clear that ID
        if (identity == null)
        {
            ClearActiveCertificate();
            return null;
        }

        ActiveCertificate = await LoadCertificate(identity);
        return ActiveCertificate;
    }

    private async Task<X509Certificate2> LoadCertificate(Models.Identity identity)
    {
        var password = await DerivePassword(identity);
        if (password == null)
            return null;

        var path = Path.GetTempFileName();
        _liteDatabase.FileStorage.Download(identity.SemanticKey, path, true);

        var certificate =
            X509Certificate2.CreateFromEncryptedPemFile(path, Encoding.UTF8.GetString(password).ToCharArray());

        File.Delete(path);

        return certificate;
    }

    public async Task<byte[]> DerivePassword(Models.Identity identity)
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
                await _fingerprint.DecryptAsync(authConfig, Convert.FromBase64String(identity.EncryptedPassword), Convert.FromBase64String(identity.EncryptedPasswordIv));
            if (!result.AuthenticationResult.Authenticated)
            {
                await Toast.Make("Could not unlock the identity").Show();
                return null;
            }

            password = result.Plaintext;
        }
        else
            password = Encoding.UTF8.GetBytes(identity.SemanticKey);

        return password;
    }

    public void ClearActiveCertificate()
    {
        ActiveCertificate = null;
        _settingsDatabase.ActiveIdentityId = null;
    }

    public async Task Activate(Models.Identity identity)
    {
        _activeCertificate = null;
        _settingsDatabase.ActiveIdentityId = identity.Id;
        ActiveCertificate = await LoadActiveCertificate();
    }
}