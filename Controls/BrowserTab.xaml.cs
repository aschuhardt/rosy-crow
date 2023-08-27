using System.ComponentModel;
using System.Windows.Input;
using Tab = RosyCrow.Models.Tab;

namespace RosyCrow.Controls;

public partial class BrowserTab : ContentView
{
    private ICommand _select;
    private Tab _tab;

    public BrowserTab()
    {
        InitializeComponent();

        Select = new Command(() => Selected?.Invoke(this, _tab));
    }

    public ICommand Select
    {
        get => _select;
        set
        {
            if (Equals(value, _select))
                return;

            _select = value;
            OnPropertyChanged();
        }
    }

    public event EventHandler<Tab> Selected;

    private void BrowserTab_OnBindingContextChanged(object sender, EventArgs e)
    {
        if (BindingContext == null)
            return;

        if (BindingContext is not Tab tab)
            throw new InvalidOperationException(
                $"BrowserTab should only be bound to a Tab; {BindingContext.GetType().Name} was bound instead!");

        if (_tab != null)
            _tab.PropertyChanged -= Tab_PropertyChanged;

        _tab = tab;
        _tab.PropertyChanged += Tab_PropertyChanged;
    }

    private async void Tab_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Tab.Selected))
        {
            if (_tab.Selected)
                await this.ScaleTo(2.0, 150, Easing.BounceIn);
            else
                await this.ScaleTo(1.0, 150, Easing.BounceIn);
        }
    }
}