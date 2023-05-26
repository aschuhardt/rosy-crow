namespace RosyCrow.Extensions;

internal static class NavigationExtensions
{
    public static async Task PushPageAsync<T>(this INavigation navigation, Action<T> configure = null)
        where T : ContentPage
    {
        var page = MauiProgram.Services.GetRequiredService<T>();
        configure?.Invoke(page);
        await navigation.PushAsync(page, true);
    }
}