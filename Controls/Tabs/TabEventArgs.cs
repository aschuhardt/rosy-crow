namespace RosyCrow.Controls.Tabs;

using Tab = Models.Tab;

public class TabEventArgs : EventArgs
{
    public TabEventArgs(Tab tab)
    {
        Tab = tab;
    }

    public Tab Tab { get; }
}