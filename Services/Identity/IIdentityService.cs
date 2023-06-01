using System.Security.Cryptography.X509Certificates;

namespace RosyCrow.Services.Identity;

public interface IIdentityService
{
    public X509Certificate2 ActiveCertificate { get; }
    bool ShouldReloadActiveCertificate { get; }
    void ClearActiveCertificate();
    Task Activate(Models.Identity identity);
    Task<X509Certificate2> LoadActiveCertificate();
    Task<X509Certificate2> LoadCertificate(Models.Identity identity);
    Task<Models.Identity> GenerateNewIdentity(string name, Func<Task<bool>> useDeviceCredentialsPrompt);

    Task<Models.Identity> ImportIdentityCertificate(string name, X509Certificate2 certificate,
        Func<Task<bool>> useDeviceCredentialsPrompt);
}