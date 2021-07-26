using CharacterMap.Controls;
using System;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace CharacterMap.Helpers
{
    [Bindable]
    public class TitleBarHelper : DependencyObject
    {
        public static UIElement GetDefaultTitleBar(DependencyObject obj)
        {
            return (UIElement)obj.GetValue(DefaultTitleBarProperty);
        }

        public static void SetDefaultTitleBar(DependencyObject obj, UIElement value)
        {
            obj.SetValue(DefaultTitleBarProperty, value);
        }

        // Using a DependencyProperty as the backing store for DefaultTitleBar.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DefaultTitleBarProperty =
            DependencyProperty.RegisterAttached("DefaultTitleBar", typeof(UIElement), typeof(TitleBarHelper), new PropertyMetadata(null));



        static WeakReference<FrameworkElement> _previousElement;

        internal static void SetTitle(string name)
        {
            ApplicationView.GetForCurrentView().Title = name ?? string.Empty;
        }

        internal static void SetTitleBar(FrameworkElement e)
        {
            SetDefaultTitleBar(Window.Current.Content, e);

            if (e is XamlTitleBar bar)
                Window.Current.SetTitleBar(bar.GetDragElement());
            else
                Window.Current.SetTitleBar(e);
        }

        internal static void SetTranisentTitleBar(FrameworkElement e)
        {
            if (GetDefaultTitleBar(Window.Current.Content) is XamlTitleBar bar)
                bar.IsDragTarget = e == bar;

            Window.Current.SetTitleBar(e);
        }

        internal static void RestoreDefaultTitleBar()
        {
            if (GetDefaultTitleBar(Window.Current.Content) is XamlTitleBar bar)
                bar.IsDragTarget = true;

            Window.Current.SetTitleBar(GetDefaultTitleBar(Window.Current.Content));
        }
    }
}
