using System.ComponentModel;
using Tab = RosyCrow.Models.Tab;

namespace RosyCrow.Controls.Tabs;

public partial class BrowserTab : TabButtonBase
{
    private Tab _tab;

    public BrowserTab() : base(true)
    {
        InitializeComponent();
    }

    public event EventHandler<Tab> AfterSelected;

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

        HandleSelectionChanged(_tab.Selected);
    }

    private void Tab_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Tab.Selected))
            HandleSelectionChanged(_tab.Selected);
    }

    public override void Tapped()
    {
        _tab.Selected = true;
        AfterSelected?.Invoke(this, _tab);
    }
}