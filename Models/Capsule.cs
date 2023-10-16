using SQLite;

namespace RosyCrow.Models;

public class Capsule
{
    [PrimaryKey] [AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true)]
    public string Host { get; set; }

    public string Icon { get; set; }
}