using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Xaml;
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
    }
}
