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
    bool SwipeEnabled { get; set; }
    bool UseCustomFontSize { get; set; }
    string CustomCss { get; set; }
    int CustomFontSizeText { get; set; }
    int CustomFontSizeH1 { get; set; }
    int CustomFontSizeH2 { get; set; }
    int CustomFontSizeH3 { get; set; }
    bool UseCustomCss { get; set; }
    bool AnnotateLinkScheme { get; set; }
}