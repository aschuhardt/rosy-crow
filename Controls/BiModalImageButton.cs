using System.Windows.Input;
using Google.Android.Material.Ripple;
using Microsoft.Maui.Handlers;

namespace RosyCrow.Controls;

/// <summary>
///     An image button with two command states; one for a short press, and another for a long press
/// </summary>
public class BiModalImageButton : ImageButton
{
    public static BindableProperty LongCommandProperty =
        BindableProperty.Create(nameof(LongCommand), typeof(ICommand), typeof(BiModalImageButton));

    public BiModalImageButton()
    {
        ImageButtonHandler.Mapper.AppendToMapping("BiModalButtonHandler", (handler, button) =>
        {
            if (button is not BiModalImageButton)
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

        e.Handled = true;
        HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);

        LongCommand?.Execute(null);
    }
#endif

    public ICommand LongCommand
    {
        get => (ICommand)GetValue(LongCommandProperty);
        set => SetValue(LongCommandProperty, value);
    }
}