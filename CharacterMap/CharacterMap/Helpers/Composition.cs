using CharacterMap.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Helpers
{
    [Bindable]
    public class Composition : DependencyObject
    {
        public const double DefaultOffsetDuration = 0.325;

        private static Dictionary<Compositor, Vector3KeyFrameAnimation> _defaultOffsetAnimations { get; }
            = new Dictionary<Compositor, Vector3KeyFrameAnimation>();

        private static string CENTRE_EXPRESSION =>
            $"({nameof(Vector3)}(this.Target.{nameof(Visual.Size)}.{nameof(Vector2.X)} * 0.5f, " +
            $"this.Target.{nameof(Visual.Size)}.{nameof(Vector2.Y)} * 0.5f, 0f))";


        #region Attached Properties

        public static Duration GetOpacityDuration(DependencyObject obj)
        {
            return (Duration)obj.GetValue(OpacityDurationProperty);
        }

        public static void SetOpacityDuration(DependencyObject obj, Duration value)
        {
            obj.SetValue(OpacityDurationProperty, value);
        }

        public static readonly DependencyProperty OpacityDurationProperty =
            DependencyProperty.RegisterAttached("OpacityDuration", typeof(Duration), typeof(Composition), new PropertyMetadata(new Duration(TimeSpan.FromSeconds(0)), (d, e) =>
            {
                if (d is FrameworkElement element && e.NewValue is Duration t)
                {
                    SetOpacityTransition(element, t.HasTimeSpan ? t.TimeSpan : TimeSpan.Zero);
                }
            }));

        #endregion


        private static void SetOpacityTransition(FrameworkElement e, TimeSpan t)
        {
            if (t.TotalMilliseconds > 0)
            {
                var c = e.GetElementVisual().Compositor;
                var ani = c.CreateScalarKeyFrameAnimation();
                ani.Target = nameof(Visual.Opacity);
                ani.InsertExpressionKeyFrame(1, "this.FinalValue", c.CreateLinearEasingFunction());
                ani.Duration = t;

                e.SetImplicitAnimation(nameof(Visual.Opacity), ani);
            }
            else
            {
                e.SetImplicitAnimation(nameof(Visual.Opacity), null);
            }
        }


        public static void PlayEntrance(UIElement target, int delayMs = 0, int fromOffset = 140)
        {
            var animation = CreateEntranceAnimation(target, new Vector3(0, 140, 0), delayMs);
            target.GetElementVisual().StartAnimationGroup(animation);
        }

        public static ICompositionAnimationBase CreateEntranceAnimation(UIElement target, Vector3 from, int delayMs, int durationMs = 1000)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(target, true);
            Compositor c = target.GetElementVisual().Compositor;

            TimeSpan delay = TimeSpan.FromMilliseconds(delayMs);
            var e = c.CreateCubicBezierEasingFunction(new Vector2(.1f, .9f), new Vector2(.2f, 1));

            var t = c.CreateVector3KeyFrameAnimation();
            t.Target = "Translation";
            t.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            t.DelayTime = delay;
            t.InsertKeyFrame(0, from);
            t.InsertKeyFrame(1, new Vector3(0), e);
            t.Duration = TimeSpan.FromMilliseconds(durationMs);

            var o = c.CreateScalarKeyFrameAnimation();
            o.Target = nameof(Visual.Opacity);
            o.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            t.DelayTime = delay;
            o.InsertKeyFrame(0, 0);
            o.InsertKeyFrame(1, 1);
            o.Duration = TimeSpan.FromMilliseconds(durationMs * 0.33);

            var g = c.CreateAnimationGroup();
            g.Add(t);
            g.Add(o);

            return g;
        }

        public static void PlayScaleEntrance(FrameworkElement target, float from, float to)
        {
            Visual v = target.GetElementVisual();

            if (target.Tag == null)
            {
                var c = v.Compositor.CreateExpressionAnimation();
                c.Target = nameof(Visual.CenterPoint);
                c.Expression = CENTRE_EXPRESSION;
                v.StartAnimationGroup(c);
                target.Tag = target;
            }

            var e = v.Compositor.CreateCubicBezierEasingFunction(new Vector2(.1f, .9f), new Vector2(.2f, 1));

            var t = v.Compositor.CreateVector3KeyFrameAnimation();
            t.Target = nameof(Visual.Scale);
            t.InsertKeyFrame(0, new Vector3(from, from, 0));
            t.InsertKeyFrame(1, new Vector3(to, to, 0), e);
            t.Duration = TimeSpan.FromSeconds(0.6);

            var o = v.Compositor.CreateScalarKeyFrameAnimation();
            o.Target = nameof(Visual.Opacity);
            o.InsertKeyFrame(0, 0);
            o.InsertKeyFrame(1, 1);
            o.Duration = TimeSpan.FromSeconds(0.2);

            var g = v.Compositor.CreateAnimationGroup();
            g.Add(t);
            g.Add(o);

            v.StartAnimationGroup(g);
        }

        public static void SetStandardReposition(object sender, RoutedEventArgs args)
        {
            UIElement e = (UIElement)sender;
            Visual v = e.GetElementVisual();

            if (!_defaultOffsetAnimations.TryGetValue(v.Compositor, out Vector3KeyFrameAnimation value))
            {
                var o = v.Compositor.CreateVector3KeyFrameAnimation();
                o.Target = nameof(Visual.Offset);
                o.InsertExpressionKeyFrame(0, "this.StartingValue");
                o.InsertExpressionKeyFrame(1, "this.FinalValue");
                o.Duration = TimeSpan.FromSeconds(DefaultOffsetDuration);
                _defaultOffsetAnimations[v.Compositor] = o;
                value = o;
            }

            v.SetImplicitAnimation(nameof(Visual.Offset), value);
        }


        

        public static void SetThemeShadow(UIElement target, float depth, params UIElement[] recievers)
        {
            if (!Utils.Supports1903 || !ResourceHelper.AppSettings.EnableShadows)
                return;

            if (!CompositionCapabilities.GetForCurrentView().AreEffectsFast())
                return;

            target.Translation = new Vector3(0, 0, depth);

            var shadow = new ThemeShadow();
            target.Shadow = shadow;
            foreach (var r in recievers)
                shadow.Receivers.Add(r);
        }





        /* Adding or removing Receivers is glitchy AF */

        //public static void TryAddRecievers(UIElement target, params UIElement[] recievers)
        //{
        //    if (!Utils.Supports1903)
        //        return;

        //    if (target.Shadow is ThemeShadow t)
        //    {
        //        foreach (var r in recievers)
        //            if (!t.Receivers.Any(c => c == r))
        //                t.Receivers.Add(r);
        //    }
        //}

        //public static void TryRemoveRecievers(UIElement target, params UIElement[] recievers)
        //{
        //    if (!Utils.Supports1903)
        //        return;

        //    if (target.Shadow is ThemeShadow t)
        //    {
        //        target.Shadow = null;
        //        ThemeShadow nt = new ThemeShadow();
        //        foreach (var s in t.Receivers)
        //        {
        //            if (!recievers.Contains(s))
        //                nt.Receivers.Add(s);
        //        }

        //        target.Shadow = nt;
        //    }
        //}
    }
}
