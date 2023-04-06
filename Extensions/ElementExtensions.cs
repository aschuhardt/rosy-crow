namespace RosyCrow.Extensions;

internal static class ElementExtensions
{
    public static ContentPage FindParentPage(this Element view, Element current = null)
    {
        current ??= view.Parent;
        if (current is ContentPage page)
            return page;

        return FindParentPage(view, current.Parent);
    }
}