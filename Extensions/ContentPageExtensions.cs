using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

// ReSharper disable AsyncVoidLambda

namespace Yarrow.Extensions;

internal static class ContentPageExtensions
{
    public static void ShowToast(this ContentPage page, string message, ToastDuration duration)
    {
        page.Dispatcher.Dispatch(async () => { await Toast.Make(message, duration).Show(); });
    }
}