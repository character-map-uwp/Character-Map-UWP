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
using System.IO.Compression;
using Windows.Storage;
using System.IO;
using Microsoft.UI.Xaml.Controls;
using CharacterMap.Models;

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

        public static MenuFlyout AddSeparator(this MenuFlyout menu, bool isVisible = true)
        {
            menu.Items.Add(new MenuFlyoutSeparator().SetVisible(isVisible));
            return menu;
        }

        public static MenuFlyoutSubItem Add(this MenuFlyoutSubItem item, BasicFontFilter filter, Style style = null)
        {
            MenuFlyoutItem i = new() { Style = style };
            Core.Properties.SetFilter(i, filter);
            item.Items.Add(i);
            return item;
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

        public static T Realize<T>(this T list, double width = 100, double height = 100) where T : ItemsControl
        {
            if (list.ItemsPanelRoot == null)
                list.Measure(new (width, height));

            return list;
        }

        public static T Merge<T>(this T dictionary, ResourceDictionary d) where T : ResourceDictionary
        {
            dictionary.MergedDictionaries.Add(d);
            return dictionary;
        }

        public static T MergeMUXC<T>(this T dictionary, ControlsResourcesVersion version) where T : ResourceDictionary
        {
            dictionary.MergedDictionaries.Add(new XamlControlsResources {  ControlsResourcesVersion = version });
            return dictionary;
        }

        public static T Merge<T>(this T dictionary, string source) where T : ResourceDictionary
        {
            dictionary.MergedDictionaries.Add(new () { Source = new Uri(source) });
            return dictionary;
        }

        public static Task<StorageFile> ExtractToFolderAsync(
            this ZipArchiveEntry entry, StorageFolder targetFolder, string fileName, CreationCollisionOption option)
        {
            return Task.Run(async () =>
            {
                using var s = entry.Open();
                var file = await targetFolder.CreateFileAsync($"{Path.GetRandomFileName()}.{Path.GetExtension(entry.Name)}", option).AsTask().ConfigureAwait(false);
                using var fs = await file.OpenStreamForWriteAsync().ConfigureAwait(false);
                s.CopyTo(fs);
                fs.Flush();

                return file;
            });
        }
    }
}
