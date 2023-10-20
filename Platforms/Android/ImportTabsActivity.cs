using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace RosyCrow.Platforms.Android;

[Activity(Label = "Import Tabs",
    Theme = "@style/Maui.SplashTheme", Exported = true,
    LaunchMode = LaunchMode.SingleTask, NoHistory = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(new [] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault },
    DataMimeType = "application/json")]
public class ImportTabsActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (Intent?.Data == null || Intent.Action != Intent.ActionView || ContentResolver == null)
            return;

        var stream = ContentResolver.OpenInputStream(Intent.Data);
        if (stream?.CanRead ?? false)
        {
            if (Microsoft.Maui.Controls.Application.Current is App app)
                _ = app.HandleImportTabsIntent(stream);
        }

        Finish();
    }
}