using System.Windows.Input;
using Microsoft.Maui.Handlers;

#if ANDROID
using View = Android.Views.View;
#endif

namespace RosyCrow.Controls;

/// <summary>
///     An image button with two command states; one for a short press, and another for a long press
/// </summary>
public class BiModalImageButton : ImageButton
{
    public static readonly BindableProperty LongCommandProperty =
        BindableProperty.Create(nameof(LongCommand), typeof(ICommand), typeof(BiModalImageButton));

    public BiModalImageButton()
    {
        ImageButtonHandler.Mapper.AppendToMapping(@"BiModalButtonHandler", (handler, button) =>
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

    public ICommand LongCommand
    {
        get => (ICommand)GetValue(LongCommandProperty);
        set => SetValue(LongCommandProperty, value);
    }

#if ANDROID
    private void PlatformView_LongClick(object sender, View.LongClickEventArgs e)
    {
        if (!LongCommand?.CanExecute(null) ?? false)
            return;

        e.Handled = true;
        HapticFeedback.Default.Perform(HapticFeedbackType.LongPress);

        LongCommand?.Execute(null);
    }
#endif
}