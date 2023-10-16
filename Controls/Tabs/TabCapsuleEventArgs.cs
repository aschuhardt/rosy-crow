using RosyCrow.Models;
using Tab = RosyCrow.Models.Tab;

namespace RosyCrow.Controls.Tabs;

public class TabCapsuleEventArgs : TabEventArgs
{
    public TabCapsuleEventArgs(Tab tab, Capsule capsule) : base(tab)
    {
        Capsule = capsule;
    }

    public Capsule Capsule { get; }
}