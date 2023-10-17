using Microsoft.Maui.Handlers;

namespace RosyCrow.Controls.Tabs;

public abstract class TabButtonBase : ContentView
{
    private const double InitialScale = 1.0;
    private const double LargerScale = 1.2;

    private readonly Animation _deselectedAnimation;
    private readonly Animation _selectedAnimation;
    private readonly Animation _triggeredAnimation;

    protected TabButtonBase(bool selectable)
    {
        Selectable = selectable;

        if (Selectable)
        {
            _selectedAnimation = new Animation(v => Scale = v, InitialScale, LargerScale, Easing.CubicInOut);
            _deselectedAnimation = new Animation(v => Scale = v, LargerScale, InitialScale, Easing.CubicInOut);
        }
        else
        {
            _triggeredAnimation = new Animation
            {
                { 0.0, 0.5, new Animation(v => Scale = v, InitialScale, LargerScale, Easing.CubicInOut) },
                { 0.5, 1.0, new Animation(v => Scale = v, LargerScale, InitialScale, Easing.CubicInOut) }
            };
        }

        HandlerChanged += TabButtonBase_HandlerChanged;
    }

    private void TabButtonBase_HandlerChanged(object sender, EventArgs e)
    {
        var view = (Handler as ContentViewHandler)?.PlatformView;
        if (view == null)
            return;

        view.Clickable = true;
        view.Click += (_, _) => Tapped();
    }

    public bool Selectable { get; }

    protected virtual void HandleSelectionChanged(bool selected)
    {
        if (selected)
            _selectedAnimation.Commit(this, nameof(_selectedAnimation), length: 200);
        else
            _deselectedAnimation.Commit(this, nameof(_deselectedAnimation), length: 200);
    }

    protected void HandleTriggered()
    {
        _triggeredAnimation.Reset(); // I *think* this is necessary due to this one child animations; otherwise it only fires once
        _triggeredAnimation.Commit(this, nameof(_triggeredAnimation), length: 250);
    }

    public abstract void Tapped();
}