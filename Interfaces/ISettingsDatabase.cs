namespace RosyCrow.Interfaces;

public interface ISettingsDatabase
{
    string HomeUrl { get; set; }
    string LastVisitedUrl { get; set; }
    bool SaveVisited { get; set; }
    string Theme { get; set; }
    int? ActiveIdentityId { get; set; }
    int HistoryPageSize { get; set; }
    bool InlineImages { get; set; }
    bool StrictTofuMode { get; set; }
}