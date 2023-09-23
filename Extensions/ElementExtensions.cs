namespace RosyCrow.Extensions;

internal static class ElementExtensions
{
    public static ContentPage FindParentPage(this Element view, Element current = null)
    {
        if (current == null && view.Parent == null)
            throw new ArgumentNullException(nameof(Element.Parent), "The current element has no parent");

        current ??= view.Parent;
        if (current is ContentPage page)
            return page;

        return FindParentPage(view, current.Parent);
    }
}