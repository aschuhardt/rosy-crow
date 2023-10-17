using System.ComponentModel;
using RosyCrow.Models;

namespace RosyCrow.Interfaces;

public interface ISettingsDatabase : INotifyPropertyChanged
{
    string HomeUrl { get; set; }
    string LastVisitedUrl { get; set; }
    bool SaveVisited { get; set; }
    string Theme { get; set; }
    int? ActiveIdentityId { get; set; }
    int HistoryPageSize { get; set; }
    bool InlineImages { get; set; }
    bool StrictTofuMode { get; set; }
    bool HidePullTab { get; set; }
    bool AllowIpv6 { get; set; }
    TabSide TabSide { get; set; }
    bool TabsEnabled { get; set; }
}