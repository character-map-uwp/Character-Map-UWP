using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;

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

[DependencyProperty<double>("Threshold", 60d)]
[DependencyProperty<ZoomTriggerMode>("Mode", ZoomTriggerMode.Threshold)]
[DependencyProperty<double>("ScaleFactor", 1d)] // Scale factor for Delta mode
public partial class ZoomHelper : DependencyObject, IAttached
{
    FrameworkElement _target = null;

    public event EventHandler ZoomInRequested;

    public event EventHandler ZoomOutRequested;

    public event EventHandler<double> ZoomRequested;

    public void Attach(FrameworkElement element)
    {
        // 1. Clear existing target
        if (_target is not null)
            _target.PointerWheelChanged -= _target_PointerWheelChanged;

        // 2. Set new target
        _target = element;

        if (_target is null)
            return;

        // 3. Hook events to target
        _target.PointerWheelChanged -= _target_PointerWheelChanged;
        _target.PointerWheelChanged += _target_PointerWheelChanged;
    }

    public void Detach(FrameworkElement target)
    {
        if (target is not null)
        {
            target.PointerWheelChanged -= _target_PointerWheelChanged;
            _target = null;
        }
    }

    private void _target_PointerWheelChanged(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // Check if the Ctrl key is pressed
        var ctrlState = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
        bool isCtrlPressed = (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

        if (isCtrlPressed)
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