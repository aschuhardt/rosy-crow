using System.Windows.Input;

namespace RosyCrow.Controls;

public partial class AdderTab : ContentView
{
    private ICommand _trigger;

    public AdderTab()
    {
        InitializeComponent();

        BindingContext = this;

        Trigger = new Command(() => Triggered?.Invoke(this, EventArgs.Empty));
    }

    public ICommand Trigger
    {
        get => _trigger;
        set
        {
            if (Equals(value, _trigger))
                return;

            _trigger = value;
            OnPropertyChanged();
        }
    }

    public event EventHandler Triggered;
}