namespace RosyCrow.Controls.Tabs;

public class UrlEventArgs : EventArgs
{
    public UrlEventArgs(Uri uri)
    {
        Uri = uri;
    }

    public Uri Uri { get; }
}