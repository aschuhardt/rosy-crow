namespace RosyCrow.Models;

public class HostCertificate
{
    public int Id { get; set; }
    public string Host { get; set; }
    public string Subject { get; set; }
    public string Issuer { get; set; }
    public string Fingerprint { get; set; }
    public DateTime Expiration { get; set; }
    public DateTime Added { get; set; }
    public DateTime Updated { get; set; }
}