namespace RosyCrow.Extensions;

internal static class NavigationExtensions
{
    public static Task PushPageAsync<T>(this INavigation navigation, Action<T> configure = null)
        where T : ContentPage
    {
        var page = MauiProgram.Services.GetRequiredService<T>();
        configure?.Invoke(page);
        return navigation.PushAsync(page, true);
    }
    public static Task PushModalPageAsync<T>(this INavigation navigation, Action<T> configure = null)
        where T : ContentPage
    {
        var page = MauiProgram.Services.GetRequiredService<T>();
        configure?.Invoke(page);
        return navigation.PushModalAsync(page, true);
    }
}