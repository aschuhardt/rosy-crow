using SQLite;

namespace RosyCrow.Models;

public class Bookmark
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    [Indexed]
    public string Url { get; set; }
    public string Title { get; set; }
    [Indexed]
    public int? Order { get; set; }
}