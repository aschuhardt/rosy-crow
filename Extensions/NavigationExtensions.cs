namespace Yarrow.Extensions;

internal static class NavigationExtensions
{
    public static async Task PushPageAsync<T>(this INavigation navigation) where T : ContentPage
    {
        await navigation.PushAsync(MauiProgram.Services.GetRequiredService<T>(), true);
    }
}