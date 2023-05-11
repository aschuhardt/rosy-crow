using Android.App;
using Android.Runtime;

namespace RosyCrow.Platforms.Android;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
        TransparentTrustProvider.Register();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
