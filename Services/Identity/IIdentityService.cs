using System.Security.Cryptography.X509Certificates;

namespace RosyCrow.Services.Identity;

public interface IIdentityService
{
    public X509Certificate2 ActiveCertificate { get; }
    bool ShouldReloadActiveCertificate { get; }
    Task<byte[]> DerivePassword(Models.Identity identity);
    void ClearActiveCertificate();
    Task Activate(Models.Identity identity);
    Task<X509Certificate2> LoadActiveCertificate();
    Task<X509Certificate2> LoadCertificate(Models.Identity identity);
}