using Tab = RosyCrow.Models.Tab;

namespace RosyCrow.Controls.Tabs;

public partial class BrowserTab : TabButtonBase
{
    public BrowserTab() : base(true)
    {
        InitializeComponent();
    }

    public event EventHandler<TabEventArgs> AfterSelected;
    public event EventHandler<TabEventArgs> RemoveRequested;

    private void BrowserTab_OnBindingContextChanged(object sender, EventArgs e)
    {
        if (BindingContext == null)
            return;

        if (BindingContext is not Tab tab)
            throw new InvalidOperationException(
                $"BrowserTab should only be bound to a {nameof(Tab)}; {BindingContext.GetType().Name} was bound instead!");

        tab.SelectedChanged = new Command<Tab>(t => HandleSelectionChanged(t.Selected));

        HandleSelectionChanged(tab.Selected);
    }

    public override void Tapped()
    {
        // if the button is tapped while selected, then delete it; otherwise, select it
        if (BindingContext is not Tab tab)
            return;

        if (!tab.Selected)
        {
            tab.Selected = true;
            AfterSelected?.Invoke(this, new TabEventArgs(tab));
        }
        else
        {
            RemoveRequested?.Invoke(this, new TabEventArgs(tab));
        }
    }
}