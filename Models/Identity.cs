namespace RosyCrow.Models;

public class Identity
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string SemanticKey { get; set; }
    public string Hash { get; set; }
    public string EncryptedPassword { get; set; }
}