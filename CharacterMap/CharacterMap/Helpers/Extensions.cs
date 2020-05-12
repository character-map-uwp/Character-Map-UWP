using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public static void SetVisible(this FrameworkElement e, bool b)
        {
            e.Visibility = b ? Visibility.Visible : Visibility.Collapsed;
        }

        public static List<UIElement> TryGetChildren(this ItemsControl control)
        {
            if (control.ItemsPanelRoot != null)
                return new List<UIElement>(control.ItemsPanelRoot.Children.ToList());

            return new List<UIElement>();
        }
    }
}
