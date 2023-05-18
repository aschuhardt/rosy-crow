using SQLite;

namespace RosyCrow.Models;

public class Setting
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    [Indexed(Unique = true)]
    public string Name { get; set; }
    public string StringValue { get; set; }
    public int IntValue { get; set; }
    public bool BoolValue { get; set; }
}