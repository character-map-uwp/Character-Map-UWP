using Microsoft.UI.Xaml.Controls;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Helpers;

public interface IAttached
{
    void Attach(FrameworkElement element);
    void Detach(FrameworkElement element);
}

public enum ZoomTriggerMode
{
    Threshold,
    Delta
}

[DependencyProperty<double>("Threshold", 60d)] // ScrollWheel threshold for triggering a "ZoomIn/Out" event
[DependencyProperty<ZoomTriggerMode>("Mode", ZoomTriggerMode.Threshold)]
[DependencyProperty<double>("ScaleFactor", 1d)] // Scale factor for Delta mode
[DependencyProperty<bool>("TriggerWhenFocused")]
public partial class ZoomHelper : DependencyObject, IAttached
{
    public const double DefaultSliderScaleFactor = 0.033d;

    public FrameworkElement Target { get; private set; }

    public event EventHandler ZoomInRequested;

    public event EventHandler ZoomOutRequested;

    public event EventHandler<double> ZoomRequested;

    public void Attach(FrameworkElement element)
    {
        /* Note: At some point this will be extended to 
         * support Pinch-to-zoom gestures on trackpads */

        // 1. Clear existing target
        if (Target is not null)
            Target.PointerWheelChanged -= _target_PointerWheelChanged;

        // 2. Set new target
        Target = element;

        if (Target is null)
            return;

        // 3. Hook events to target
        Target.PointerWheelChanged -= _target_PointerWheelChanged;
        Target.PointerWheelChanged += _target_PointerWheelChanged;
    }

   

    public void Detach(FrameworkElement target)
    {
        if (target is not null)
        {
            target.PointerWheelChanged -= _target_PointerWheelChanged;
            Target = null;
        }
    }

    private void _target_PointerWheelChanged(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // Check if the Ctrl key is pressed
        var ctrlState = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
        bool isCtrlPressed = (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

        if (isCtrlPressed 
            || (TriggerWhenFocused && Target is Control c && c.ContainsFocus()))
        {
            // Get the delta of the scroll wheel
            var pointerPoint = e.GetCurrentPoint(sender as FrameworkElement);
            int delta = pointerPoint.Properties.MouseWheelDelta;

            if (Mode is ZoomTriggerMode.Threshold)
            {
                if (delta > Threshold)
                    this.ZoomInRequested?.Invoke(this, EventArgs.Empty);
                else if (delta < -Threshold)
                    this.ZoomOutRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (Mode is ZoomTriggerMode.Delta)
            {
                this.ZoomRequested?.Invoke(this, delta * ScaleFactor);
            }

            e.Handled = true;
        }
    }
}