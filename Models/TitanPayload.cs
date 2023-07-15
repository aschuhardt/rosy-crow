namespace RosyCrow.Models;

public class TitanPayload
{
    public string MimeType { get; set; }
    public string Token { get; set; }
    public int Size { get; set; }
    public Stream Contents { get; set; }
}