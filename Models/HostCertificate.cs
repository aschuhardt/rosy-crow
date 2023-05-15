using SQLite;

namespace RosyCrow.Models;

public class HostCertificate
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public bool Accepted { get; set; }
    [Indexed(Unique = true)]
    public string Host { get; set; }
    public string Subject { get; set; }
    public string Issuer { get; set; }
    public string Fingerprint { get; set; }
    public DateTime Expiration { get; set; }
    public DateTime Added { get; set; }
    public DateTime Updated { get; set; }
}