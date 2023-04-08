using System.Windows.Input;
using Google.Android.Material.Ripple;
using Microsoft.Maui.Handlers;

namespace RosyCrow.Controls;

/// <summary>
///     An image button with two command states; one for a short press, and another for a long press
/// </summary>
public class BiModalButton : ImageButton
{
    public static BindableProperty LongCommandProperty =
        BindableProperty.Create(nameof(LongCommand), typeof(ICommand), typeof(BiModalButton));

    public BiModalButton()
    {
        ImageButtonHandler.Mapper.AppendToMapping("BiModalButtonHandler", (handler, button) =>
        {
            if (button is not BiModalButton)
                return;

#if ANDROID
            var view = handler.PlatformView;
            view.LongClickable = true;
            view.LongClick += PlatformView_LongClick;
#endif
        });
    }

#if ANDROID
    private void PlatformView_LongClick(object sender, Android.Views.View.LongClickEventArgs e)
    {
        if (!LongCommand?.CanExecute(null) ?? false)
            return;

        LongCommand?.Execute(null);
    }
#endif

    public ICommand LongCommand
    {
        get => (ICommand)GetValue(LongCommandProperty);
        set => SetValue(LongCommandProperty, value);
    }
}