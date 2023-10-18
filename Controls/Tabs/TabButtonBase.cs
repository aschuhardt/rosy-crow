using Microsoft.Maui.Handlers;

namespace RosyCrow.Controls.Tabs;

public abstract class TabButtonBase : ContentView
{
    protected TabButtonBase()
    {
        HandlerChanged += TabButtonBase_HandlerChanged;
    }

    private void TabButtonBase_HandlerChanged(object sender, EventArgs e)
    {
#if ANDROID
        var view = (Handler as ContentViewHandler)?.PlatformView;
        if (view == null)
            return;

        view.Clickable = true;
        view.Click += (_, _) => Tapped();
#endif
    }

    public abstract void Tapped();
}