using System.Windows.Input;

namespace Yarrow.Controls;

/// <summary>
///     An image button with two command states; one for a short press, and another for a long press
/// </summary>
public class BiModalButton : ImageButton
{
    public static BindableProperty ShortCommandProperty =
        BindableProperty.Create(nameof(ShortCommand), typeof(ICommand), typeof(BiModalButton));

    public static BindableProperty LongCommandProperty =
        BindableProperty.Create(nameof(LongCommand), typeof(ICommand), typeof(BiModalButton));

    public static BindableProperty LongPressDurationProperty =
        BindableProperty.Create(nameof(LongPressDuration), typeof(int), typeof(BiModalButton), 2000);

    private bool _longPressTimedOut;
    private Timer _timer;

    public BiModalButton()
    {
        LongPressDuration = 1500;
        Pressed += OnPressed;
        Released += OnReleased;
    }

    public ICommand ShortCommand
    {
        get => (ICommand)GetValue(ShortCommandProperty);
        set => SetValue(ShortCommandProperty, value);
    }

    public ICommand LongCommand
    {
        get => (ICommand)GetValue(LongCommandProperty);
        set => SetValue(LongCommandProperty, value);
    }

    public int LongPressDuration
    {
        get => (int)GetValue(LongPressDurationProperty);
        set => SetValue(LongPressDurationProperty, value);
    }

    private void OnReleased(object sender, EventArgs e)
    {
        _timer?.Dispose();
        if (!_longPressTimedOut)
            ShortCommand?.Execute(CommandParameter);
    }

    private void OnPressed(object sender, EventArgs e)
    {
        _longPressTimedOut = false;
        _timer = new Timer(OnLongPressTimeout, null, LongPressDuration, Timeout.Infinite);
    }

    public void OnLongPressTimeout(object state)
    {
        _timer?.Dispose();
        _longPressTimedOut = true;
        LongCommand?.Execute(CommandParameter);
    }
}