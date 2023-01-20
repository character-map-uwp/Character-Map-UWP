using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace CharacterMap.Helpers
{
    public class FluentAnimationHelper
    {
        private readonly VisualStateGroup _group;
        private BackEase _pressEase = null;
        private BackEase _hoverEase = null;

        private Storyboard _hover = null;
        private Storyboard _pressDown = null;
        private Storyboard _pressUp = null;

        public FluentAnimationHelper(VisualStateGroup group)
        {
            _group = group;
            if (ResourceHelper.SupportFluentAnimation is false)
                return;

            _group.CurrentStateChanging += OnStateChanging;
        }

        internal void SetPointerTarget(FrameworkElement t)
        {
            SetTarget(_hover, t);
        }

        internal void SetPressTarget(FrameworkElement t)
        {
            SetTarget(_pressDown, t);
            SetTarget(_pressUp, t);
        }

        internal void SetPointerDownScale(double d)
        {
            if (_pressDown is Storyboard s)
            {
                s.Stop();
                foreach (var child in s.Children.OfType<DoubleAnimationUsingKeyFrames>())
                    child.KeyFrames[0].Value = d;
            }
        }

        static void SetTarget(Storyboard s, FrameworkElement ele)
        {
            if (s is null)
                return;

            s.Stop();
            ele.GetCompositeTransform();
            foreach (var child in s.Children)
                Storyboard.SetTarget(child, ele);
        }

        private void OnStateChanging(object sender, VisualStateChangedEventArgs e)
        {
            if (ResourceHelper.AllowAnimation is false
                || ResourceHelper.SupportFluentAnimation is false)
                return;

            // TODO: We can cache eases per Dispatcher

            // 1. Handle "PointerOver"
            if (e.NewState is VisualState v
                && e.OldState is VisualState old
                && old.Name is "Normal" or "Disabled"
                && v.Name is "PointerOver"
                && ResourceHelper.UsePointerOverAnimations
                && FluentAnimation.GetUsePointerOver(e.Control)
                && FluentAnimation.GetPointerOver(_group) is FrameworkElement target)
            {
                if (_hover is null)
                {
                    _hoverEase ??= new() { Amplitude = 0.5, EasingMode = EasingMode.EaseOut };
                    _hover = new();
                    _hover.CreateTimeline<DoubleAnimationUsingKeyFrames>(target, TargetProperty.CompositeTransform.TranslateY)
                        .AddKeyFrame(0.15, -2)
                        .AddKeyFrame(0.5, 0, _hoverEase);
                }

                Play(_hover);
            }

            // 2. Handle "PressedDown"
            if (e.NewState is VisualState vp
                && vp.Name.StartsWith("Pressed")
                && e.OldState is VisualState ov
                && ov.Name.StartsWith("Pressed") is false
                && FluentAnimation.GetPressed(_group) is FrameworkElement pressedTarget)
            {
                if (_pressDown is null)
                {
                    _pressDown = new();
                    double scale = FluentAnimation.GetPointerDownScale(e.Control);
                    _pressDown.CreateTimeline<DoubleAnimationUsingKeyFrames>(pressedTarget, TargetProperty.CompositeTransform.ScaleX)
                        .AddKeyFrame(0.1, scale);
                    _pressDown.CreateTimeline<DoubleAnimationUsingKeyFrames>(pressedTarget, TargetProperty.CompositeTransform.ScaleY)
                        .AddKeyFrame(0.1, scale);
                }

                Play(_pressDown);
            }
            // 3. Handle "PressedReleased"
            else if (e.OldState is VisualState oldP
                    && oldP.Name.StartsWith("Pressed")
                    && e.NewState is VisualState ns
                    && ns.Name.StartsWith("Pressed") is false
                    && FluentAnimation.GetPressed(_group) is FrameworkElement f)
            {
                if (_pressUp is null)
                {
                    _pressUp = new();
                    double duration = 0.35;
                    _pressEase ??= new() { Amplitude = 1, EasingMode = EasingMode.EaseOut };
                    _pressUp.CreateTimeline<DoubleAnimationUsingKeyFrames>(f, TargetProperty.CompositeTransform.ScaleX)
                        .AddKeyFrame(duration, 1, _pressEase);
                    _pressUp.CreateTimeline<DoubleAnimationUsingKeyFrames>(f, TargetProperty.CompositeTransform.ScaleY)
                        .AddKeyFrame(duration, 1, _pressEase);
                }

                Play(_pressUp);
            }

            static void Play(Storyboard s)
            {
                s.Begin();
                if (ResourceHelper.AllowAnimation is false)
                    s.SkipToFill();
            }
        }

    }

    [Bindable]
    public class FluentAnimation
    {
        #region Key 

        private static string GetKey(DependencyObject obj)
        {
            return (string)obj.GetValue(KeyProperty);
        }

        private static void SetKey(DependencyObject obj, string value)
        {
            obj.SetValue(KeyProperty, value);
        }

        public static readonly DependencyProperty KeyProperty =
            DependencyProperty.RegisterAttached("Key", typeof(string), typeof(FluentAnimation), new PropertyMetadata(null));

        #endregion

        #region Helper

        private static FluentAnimationHelper GetHelper(DependencyObject obj)
        {
            return (FluentAnimationHelper)obj.GetValue(HelperProperty);
        }

        private static void SetHelper(DependencyObject obj, FluentAnimationHelper value)
        {
            obj.SetValue(HelperProperty, value);
        }

        public static readonly DependencyProperty HelperProperty =
            DependencyProperty.RegisterAttached("Helper", typeof(FluentAnimationHelper), typeof(FluentAnimation), new PropertyMetadata(null));

        #endregion

        #region PointerDownScale

        public static double GetPointerDownScale(DependencyObject obj)
        {
            return (double)obj.GetValue(PointerDownScaleProperty);
        }

        public static void SetPointerDownScale(DependencyObject obj, double value)
        {
            obj.SetValue(PointerDownScaleProperty, value);
        }

        public static readonly DependencyProperty PointerDownScaleProperty =
            DependencyProperty.RegisterAttached("PointerDownScale", typeof(double), typeof(FluentAnimation), new PropertyMetadata(0.94d, (d,a) =>
            {
                if (a.NewValue is double scale 
                    && d is VisualStateGroup group 
                    && GetHelper(group) is FluentAnimationHelper helper)
                {
                    helper.SetPointerDownScale(scale);
                }
                else if (a.NewValue is double sc
                    && d is FrameworkElement f
                    && VisualTreeHelperExtensions.GetVisualStateGroup(f, "CommonStates") is VisualStateGroup grp
                    && GetHelper(grp) is FluentAnimationHelper hp)
                {
                    hp.SetPointerDownScale(sc);
                }
            }));

        #endregion

        #region Pressed

        public static FrameworkElement GetPressed(DependencyObject obj)
        {
            return (FrameworkElement)obj.GetValue(PressedProperty);
        }

        public static void SetPressed(DependencyObject obj, FrameworkElement value)
        {
            obj.SetValue(PressedProperty, value);
        }

        public static readonly DependencyProperty PressedProperty =
            DependencyProperty.RegisterAttached("Pressed", typeof(FrameworkElement), typeof(FluentAnimation), new PropertyMetadata(null, (d,a) =>
            {
                if (d is VisualStateGroup group && a.NewValue is FrameworkElement t)
                {
                    if (GetHelper(group) is FluentAnimationHelper helper)
                        helper.SetPressTarget(t);
                    else
                        SetHelper(group, new(group));
                }
            }));

        #endregion

        #region PointerOver

        public static FrameworkElement GetPointerOver(DependencyObject obj)
        {
            return (FrameworkElement)obj.GetValue(PointerOverProperty);
        }

        public static void SetPointerOver(DependencyObject obj, FrameworkElement value)
        {
            obj.SetValue(PointerOverProperty, value);
        }

        public static readonly DependencyProperty PointerOverProperty =
            DependencyProperty.RegisterAttached("PointerOver", typeof(FrameworkElement), typeof(FluentAnimation), new PropertyMetadata(null, (d, a) =>
            {
                if (d is VisualStateGroup group && a.NewValue is FrameworkElement t)
                {
                    if (GetHelper(group) is FluentAnimationHelper helper)
                        helper.SetPointerTarget(t);
                    else
                        SetHelper(group, new(group));
                }
            }));

        #endregion

        #region UsePointerOver

        public static bool GetUsePointerOver(DependencyObject obj)
        {
            return (bool)obj.GetValue(UsePointerOverProperty);
        }

        public static void SetUsePointerOver(DependencyObject obj, bool value)
        {
            obj.SetValue(UsePointerOverProperty, value);
        }

        public static readonly DependencyProperty UsePointerOverProperty =
            DependencyProperty.RegisterAttached("UsePointerOver", typeof(bool), typeof(FluentAnimation), new PropertyMetadata(true));

        #endregion
    }
}
