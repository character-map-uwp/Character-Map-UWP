using CharacterMap.Controls;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Styles;

public sealed partial class Controls : ResourceDictionary
{
    public Controls()
    {
        this.InitializeComponent();
    }

    public static bool GetEnableSlideOut(DependencyObject obj)
    {
        return (bool)obj.GetValue(EnableSlideOutProperty);
    }

    public static void SetEnableSlideOut(DependencyObject obj, bool value)
    {
        obj.SetValue(EnableSlideOutProperty, value);
    }

    public static readonly DependencyProperty EnableSlideOutProperty =
        DependencyProperty.RegisterAttached("EnableSlideOut", typeof(bool), typeof(Controls), new PropertyMetadata(false, (d, e) =>
    {
        if (d is FrameworkElement f && e.NewValue is bool b)
            UpdateSlideOut(f, b);

        static void UpdateSlideOut(FrameworkElement fe, bool enable)
        {
            Visual v = fe.EnableCompositionTranslation().GetElementVisual();
            if (enable && ResourceHelper.AllowAnimation)
            {
                fe.SetHideAnimation(CompositionFactory.CreateSlideOut(fe, -256, 0));
                fe.SetShowAnimation(
                    v.GetCached("_PaneImpShow", () =>
                        v.CreateVector3KeyFrameAnimation(CompositionFactory.TRANSLATION)
                            .AddKeyFrame(0, "this.StartingValue")
                            .AddKeyFrame(1, 0)
                            .SetDuration(CompositionFactory.DefaultOffsetDuration)));
            }
            else
            {
                fe.SetHideAnimation(null);
                fe.SetShowAnimation(null);
            }
        }
    }));

    private void ContentPresenter_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement f 
            && f.GetFirstAncestorOfType<ListViewBaseHeaderItem>() is { } hi
            && f.GetFirstAncestorOfType<ExtendedListView>() is { } lv)
        {
            lv.RegisterHeader(hi);
        }
    }
}
