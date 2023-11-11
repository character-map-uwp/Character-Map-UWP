using CharacterMap.Controls;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace CharacterMap.Helpers;

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

    public static readonly DependencyProperty DefaultTitleBarProperty =
        DependencyProperty.RegisterAttached("DefaultTitleBar", typeof(UIElement), typeof(TitleBarHelper), new PropertyMetadata(null));



    internal static void SetTitle(string name)
    {
        name = ApplicationView.GetForCurrentView().Title = name ?? string.Empty;
        WeakReferenceMessenger.Default.Send(name, "TitleUpdated");
    }

    internal static string GetTitle()
    {
        return ApplicationView.GetForCurrentView().Title;
    }

    internal static void SetTitleBar(UIElement e)
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

        SetTitleBar(GetDefaultTitleBar(Window.Current.Content));
    }
}
