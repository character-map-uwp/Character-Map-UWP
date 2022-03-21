using CharacterMapCX;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace CharacterMap.Helpers
{
    public static class Extensions
    {
        /// <summary>
        /// Creates a copy if the axis list with "new" instances of each axis with matching values.
        /// </summary>
        /// <param name="axis"></param>
        /// <returns></returns>
        public static IReadOnlyList<DWriteFontAxis> Copy(this IEnumerable<DWriteFontAxis> axis)
        {
            return axis.Select(a => a.WithValue(a.Value)).ToList();
        }

        public static IReadOnlyList<DWriteFontAxis> CopyDefaults(this IEnumerable<DWriteFontAxis> axis)
        {
            return axis.Select(a => a.WithValue(a.AxisDefault)).ToList();
        }

        public static void AddSorted<T>(this IList<T> list, T item, IComparer<T> comparer = null)
        {
            if (comparer == null)
                comparer = Comparer<T>.Default;

            int i = 0;
            while (i < list.Count && comparer.Compare(list[i], item) < 0)
                i++;

            list.Insert(i, item);
        }

        public static Task ExecuteAsync(this CoreDispatcher d, Func<Task> action, CoreDispatcherPriority p = CoreDispatcherPriority.Normal)
        {
            TaskCompletionSource<bool> tcs = new ();

            _ =d.RunAsync(p, async () =>
            {
                try
                {
                    await action();
                    tcs.SetResult(true);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });
            
            return tcs.Task;
        }

        public static Task ExecuteAsync(this CoreDispatcher d, Action action, CoreDispatcherPriority p = CoreDispatcherPriority.Normal)
        {
            TaskCompletionSource<bool> tcs = new ();

            _ = d.RunAsync(p, () =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });

            return tcs.Task;
        }

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

        public static T Realize<T>(this T list) where T : ItemsControl
        {
            if (list.ItemsPanelRoot == null)
                list.Measure(new (100, 100));

            return list;
        }
    }
}
