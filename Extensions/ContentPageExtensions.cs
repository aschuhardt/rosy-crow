using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Extensions;

internal static class ContentPageExtensions
{
    private static readonly object _alertLock = new();
    public static void ShowToast(this ContentPage page, string message, ToastDuration duration)
    {
        page.Dispatcher.Dispatch(async () => { await Toast.Make(message, duration).Show(); });
    }

    public static Task<bool> DisplayAlertOnMainThread(this ContentPage page, string title, string message, string cancel, string accept)
    {
        lock (_alertLock)
        {
            return MainThread.IsMainThread
                ? page.DisplayAlert(title, message, cancel, accept)
                : MainThread.InvokeOnMainThreadAsync(() => page.DisplayAlert(title, message, cancel, accept));
        }
    }

    public static Task DisplayAlertOnMainThread(this ContentPage page, string title, string message, string cancel)
    {
        if (!Monitor.TryEnter(_alertLock))
            return Task.CompletedTask;

        var result = MainThread.IsMainThread
            ? page.DisplayAlert(title, message, cancel)
            : MainThread.InvokeOnMainThreadAsync(() => page.DisplayAlert(title, message, cancel));

        Monitor.Exit(_alertLock);
        return result;
    }

    public static Task<string> DisplayPromptOnMainThread(this ContentPage page, string title, string message, string accept = "OK",
        string cancel = "Cancel", string placeholder = null, Keyboard keyboard = null, int maxLength = -1)
    {
        lock (_alertLock)
        {
            return MainThread.IsMainThread
                ? page.DisplayPromptAsync(title, message, accept, cancel, placeholder, maxLength)
                : MainThread.InvokeOnMainThreadAsync(() => page.DisplayPromptAsync(title, message, accept, cancel, placeholder, maxLength));
        }
    }
}