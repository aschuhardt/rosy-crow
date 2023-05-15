using SQLite;

namespace RosyCrow.Models;

public class Visited
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Url { get; set; }
    public string Title { get; set; }
    [Indexed]
    public DateTime Timestamp { get; set; }
}