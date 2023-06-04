using Android.Views;
using Java.Interop;
using Object = Java.Lang.Object;

namespace RosyCrow.Platforms.Android;

internal class ActionMenuClickHandler<T> : Object, IMenuItemOnMenuItemClickListener
{
    private readonly Action<T> _action;
    private readonly T _argument;

    public ActionMenuClickHandler(T argument, Action<T> action)
    {
        _argument = argument;
        _action = action;
    }

    public IntPtr Handle { get; }

    public bool OnMenuItemClick(IMenuItem item)
    {
        _action?.Invoke(_argument);
        return true;
    }
}

internal class ActionMenuClickHandler : Object, IMenuItemOnMenuItemClickListener
{
    private readonly Action _action;

    public ActionMenuClickHandler(Action action)
    {
        _action = action;
    }

    public IntPtr Handle { get; }

    public bool OnMenuItemClick(IMenuItem item)
    {
        _action?.Invoke();
        return true;
    }
}