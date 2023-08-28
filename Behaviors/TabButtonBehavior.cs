using RosyCrow.Controls.Tabs;

namespace RosyCrow.Behaviors;

internal class TabButtonBehavior : Behavior<TabButtonBase>
{
    protected override void OnAttachedTo(TabButtonBase button)
    {
        base.OnAttachedTo(button);

        button.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => PerformTapped(button))
        });
    }

    private static void PerformTapped(TabButtonBase button)
    {
        button.Tapped();
    }
}