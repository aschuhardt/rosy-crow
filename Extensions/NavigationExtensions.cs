namespace Yarrow.Extensions;

internal static class NavigationExtensions
{
    public static Task PushPageAsync<T>(this INavigation navigation) where T : ContentPage
    {
        return navigation.PushAsync(MauiProgram.Services.GetRequiredService<T>(), true);
    }
}