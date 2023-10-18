// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Controls.Tabs;

public partial class AdderTab : TabButtonBase
{
    public event EventHandler Triggered;

    public AdderTab()
    {
        InitializeComponent();
    }

    public override void Tapped()
    {
        if (!IsEnabled)
            return;

        Triggered?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPropertyChanged(string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == nameof(IsEnabled))
        {
            var opacity = IsEnabled ? 1.0 : 0.5;
            Dispatcher.Dispatch(async () => await this.FadeTo(opacity, 150));
        }
    }
}