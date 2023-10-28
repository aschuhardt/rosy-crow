using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace RosyCrow.Extensions;

internal static class CertificateExtensions
{
    private const int EncryptionIterations = 100;

    public static async Task<bool> WriteCertificate(this X509Certificate2 certificate, TextWriter writer,
        byte[] password = null)
    {
        try
        {
            await writer.WriteLineAsync(PemEncoding.Write(@"CERTIFICATE", certificate.RawData));

            if (password != null)
            {
                var convertedPassword = Encoding.UTF8.GetString(password).ToCharArray();
                var key = certificate.GetRSAPrivateKey()?.ExportEncryptedPkcs8PrivateKey(convertedPassword,
                    new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA512,
                        EncryptionIterations));
                await writer.WriteLineAsync(PemEncoding.Write(@"ENCRYPTED PRIVATE KEY", key));
            }
            else
            {
                var key = certificate.GetRSAPrivateKey()?.ExportPkcs8PrivateKey();
                await writer.WriteLineAsync(PemEncoding.Write(@"PRIVATE KEY", key));
            }

            await writer.FlushAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }
}