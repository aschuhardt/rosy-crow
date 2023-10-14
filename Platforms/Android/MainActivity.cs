using Android.App;
using Android.Content;
using Android.Content.PM;
using RosyCrow.Models;

namespace RosyCrow.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleInstance,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryBrowsable, Intent.CategoryDefault },
    DataScheme = Constants.GeminiScheme)]
[IntentFilter(new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryBrowsable, Intent.CategoryDefault },
    DataScheme = Constants.TitanScheme)]
[IntentFilter(new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryBrowsable, Intent.CategoryDefault },
    DataScheme = Constants.InternalScheme)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnNewIntent(Intent intent)
    {
        if (intent.Action == Intent.ActionView && intent.Data != null)
            (Microsoft.Maui.Controls.Application.Current as App)?.HandleNavigationIntent(intent.Data.ToString());

        base.OnNewIntent(intent);
    }
}