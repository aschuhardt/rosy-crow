using Android.App;
using Android.Content;
using Android.Content.PM;

namespace RosyCrow.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryAppBrowser }, DataScheme = "gemini")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnNewIntent(Intent intent)
    {
        if (intent.Action == Intent.ActionView && intent.Data != null)
            App.StartupUri = intent.Data.ToString();

        base.OnNewIntent(intent);
    }
}
