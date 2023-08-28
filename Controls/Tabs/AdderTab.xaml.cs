namespace RosyCrow.Controls.Tabs;

public partial class AdderTab : TabButtonBase
{
    public event EventHandler Triggered;

    public override void Tapped()
    {
        HandleTriggered();
        Triggered?.Invoke(this, EventArgs.Empty);
    }

    public AdderTab() : base(false)
    {
        InitializeComponent();
    }
}