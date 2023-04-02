using Android.App;
using Android.Runtime;
using Yarrow.Platforms.Android;

namespace Yarrow;

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
