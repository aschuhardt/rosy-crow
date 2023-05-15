using Object = Java.Lang.Object;
using WebView = Android.Webkit.WebView;

namespace RosyCrow.Platforms.Android;

internal class CallbackFindListener : Object, WebView.IFindListener
{
    private readonly Action<int> _callback;

    public CallbackFindListener(Action<int> callback)
    {
        _callback = callback;
    }

    public new IntPtr Handle => PeerReference.Handle;

    public void OnFindResultReceived(int activeMatchOrdinal, int numberOfMatches, bool isDoneCounting)
    {
        if (isDoneCounting)
            _callback?.Invoke(numberOfMatches);
    }
}