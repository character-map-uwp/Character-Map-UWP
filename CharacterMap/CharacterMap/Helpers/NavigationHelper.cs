using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace CharacterMap.Helpers;

public class NavigationHelper
{
    public event EventHandler BackRequested;

    public void Activate()
    {
        Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated -= CoreDispatcher_AcceleratorKeyActivated;
        Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated += CoreDispatcher_AcceleratorKeyActivated;

        Window.Current.CoreWindow.PointerPressed -= CoreWindow_PointerPressed;
        Window.Current.CoreWindow.PointerPressed += CoreWindow_PointerPressed;
    }

    public void Deactivate()
    {
        Window.Current.CoreWindow.Dispatcher.AcceleratorKeyActivated -= CoreDispatcher_AcceleratorKeyActivated;
        Window.Current.CoreWindow.PointerPressed -= CoreWindow_PointerPressed;
    }

    private void CoreWindow_PointerPressed(CoreWindow sender, PointerEventArgs e)
    {
        var properties = e.CurrentPoint.Properties;

        // Ignore button chords with the left, right, and middle buttons
        if (properties.IsLeftButtonPressed || properties.IsRightButtonPressed ||
            properties.IsMiddleButtonPressed) return;

        // If back or forward are pressed (but not both) navigate appropriately
        bool backPressed = properties.IsXButton1Pressed;
        bool forwardPressed = properties.IsXButton2Pressed;
        if (backPressed ^ forwardPressed)
        {
            e.Handled = true;
            if (backPressed) BackRequested?.Invoke(this, EventArgs.Empty);
            //if (forwardPressed) this.GoForwardCommand.Execute(null);
        }
    }

    private void CoreDispatcher_AcceleratorKeyActivated(CoreDispatcher sender,
        AcceleratorKeyEventArgs e)
    {
        var virtualKey = e.VirtualKey;

        // Only investigate further when Left, Right, or the dedicated Previous or Next keys
        // are pressed
        if ((e.EventType == CoreAcceleratorKeyEventType.SystemKeyDown ||
            e.EventType == CoreAcceleratorKeyEventType.KeyDown) &&
            (virtualKey == VirtualKey.Left || virtualKey == VirtualKey.Right ||
            (int)virtualKey == 166 || (int)virtualKey == 167))
        {
            var coreWindow = Window.Current.CoreWindow;
            var downState = CoreVirtualKeyStates.Down;
            bool menuKey = (coreWindow.GetKeyState(VirtualKey.Menu) & downState) == downState;
            bool controlKey = (coreWindow.GetKeyState(VirtualKey.Control) & downState) == downState;
            bool shiftKey = (coreWindow.GetKeyState(VirtualKey.Shift) & downState) == downState;
            bool noModifiers = !menuKey && !controlKey && !shiftKey;
            bool onlyAlt = menuKey && !controlKey && !shiftKey;

            if (((int)virtualKey == 166 && noModifiers) ||
                (virtualKey == VirtualKey.Left && onlyAlt))
            {
                // When the previous key or Alt+Left are pressed navigate back
                e.Handled = true;
                BackRequested?.Invoke(this, EventArgs.Empty);
            }
            //else if (((int)virtualKey == 167 && noModifiers) ||
            //    (virtualKey == VirtualKey.Right && onlyAlt))
            //{
            //    // When the next key or Alt+Right are pressed navigate forward
            //    e.Handled = true;
            //    this.GoForwardCommand.Execute(null);
            //}
        }
    }
}
