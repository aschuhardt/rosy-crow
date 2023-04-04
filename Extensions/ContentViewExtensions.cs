namespace RosyCrow.Extensions;

internal static class ContentViewExtensions
{
    public static ContentPage FindParentPage(this ContentView view, Element current = null)
    {
        current ??= view.Parent;
        if (current is ContentPage page)
            return page;

        return FindParentPage(view, current.Parent);
    }
}