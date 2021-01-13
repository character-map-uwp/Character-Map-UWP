using System.Collections.Generic;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace CharacterMap.Helpers
{
    public static class Extensions
    {
        public static T AddKeyboardAccelerator<T>(this T u, VirtualKey key, VirtualKeyModifiers modifiers) where T : UIElement
        {
            u.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = key, Modifiers = modifiers });
            return u;
        }

        public static T SetVisible<T>(this T e, bool b) where T : FrameworkElement
        {
            e.Visibility = b ? Visibility.Visible : Visibility.Collapsed;
            return e;
        }

        public static List<UIElement> TryGetChildren(this ItemsControl control)
        {
            //if (control.ItemsPanelRoot is null) // Calling measure forces ItemsPanelRoot to become inflated
            //    control.Measure(new Windows.Foundation.Size(100, 100));

            return new List<UIElement> { control };
        }
    }
}
