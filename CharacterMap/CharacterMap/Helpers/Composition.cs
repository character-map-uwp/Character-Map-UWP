using CharacterMap.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Composition;
using Windows.UI.ViewManagement;
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

        public static UISettings UISettings { get; }

        private static Dictionary<Compositor, Vector3KeyFrameAnimation> _defaultOffsetAnimations { get; }
            = new Dictionary<Compositor, Vector3KeyFrameAnimation>();

        private static string CENTRE_EXPRESSION =>
            $"({nameof(Vector3)}(this.Target.{nameof(Visual.Size)}.{nameof(Vector2.X)} * 0.5f, " +
            $"this.Target.{nameof(Visual.Size)}.{nameof(Vector2.Y)} * 0.5f, 0f))";

        public const string TRANSLATION = "Translation";
        public const string STARTING_VALUE = "this.StartingValue";
        public const string FINAL_VALUE = "this.FinalValue";
        public const int DEFAULT_STAGGER_MS = 83;

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

        static Composition()
        {
            UISettings = new UISettings();
        }

        private static void SetOpacityTransition(FrameworkElement e, TimeSpan t)
        {
            if (!UISettings.AnimationsEnabled)
                return;

            if (t.TotalMilliseconds > 0)
            {
                var c = e.GetElementVisual().Compositor;
                var ani = c.CreateScalarKeyFrameAnimation();
                ani.Target = nameof(Visual.Opacity);
                ani.InsertExpressionKeyFrame(1, FINAL_VALUE, c.CreateLinearEasingFunction());
                ani.Duration = t;

                e.SetImplicitAnimation(nameof(Visual.Opacity), ani);
            }
            else
            {
                e.SetImplicitAnimation(nameof(Visual.Opacity), null);
            }
        }

        public static void SetupOverlayPanelAnimation(UIElement e)
        {
            if (!Composition.UISettings.AnimationsEnabled)
                return;

            Visual v = e.EnableTranslation(true).GetElementVisual();

            var t = v.Compositor.CreateVector3KeyFrameAnimation();
            t.Target = Composition.TRANSLATION;
            t.InsertKeyFrame(1, new Vector3(0, 200, 0));
            t.Duration = TimeSpan.FromSeconds(0.375);

            var o = Composition.CreateFade(v.Compositor, 0, null, 200);
            e.SetHideAnimation(v.Compositor.CreateAnimationGroup(t, o));
            e.SetShowAnimation(Composition.CreateEntranceAnimation(e, new Vector3(0, 200, 0), 0, 550));
        }

        public static void PlayEntrance(UIElement target, int delayMs = 0, int fromOffsetY = 140, int fromOffsetX = 0, int durationMs = 880)
        {
            if (!UISettings.AnimationsEnabled)
                return;

            var animation = CreateEntranceAnimation(target, new Vector3(fromOffsetX, fromOffsetY, 0), delayMs, durationMs);
            target.GetElementVisual().StartAnimationGroup(animation);
        }

        public static ICompositionAnimationBase CreateEntranceAnimation(UIElement target, Vector3 from, int delayMs, int durationMs = 1000)
        {
            Compositor c = target.EnableTranslation(true).GetElementVisual().Compositor;

            TimeSpan delay = TimeSpan.FromMilliseconds(delayMs);
            var e = c.CreateEntranceEasingFunction();

            var t = c.CreateVector3KeyFrameAnimation();
            t.Target = TRANSLATION;
            t.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            t.DelayTime = delay;
            t.InsertKeyFrame(0, from);
            t.InsertKeyFrame(1, new Vector3(0), e);
            t.Duration = TimeSpan.FromMilliseconds(durationMs);

            var o = CreateFade(c, 1, 0, (int)(durationMs * 0.33), delayMs);

            return c.CreateAnimationGroup(t, o);
        }

        public static void PlayEntrance(List<UIElement> targets, int delayMs = 0, int fromOffsetY = 140, int fromOffsetX = 0, int durationMs = 880, int staggerMs = 83)
        {
            if (!UISettings.AnimationsEnabled)
                return;

            int start = delayMs;

            foreach (var target in targets)
            {
                var animation = CreateEntranceAnimation(target, new Vector3(fromOffsetX, fromOffsetY, 0), start, durationMs);
                target.GetElementVisual().StartAnimationGroup(animation);
                start += staggerMs;
            }
        }

        public static CompositionAnimation CreateFade(Compositor c, float to, float? from, int durationMs, int delayMs = 0)
        {
            var o = c.CreateScalarKeyFrameAnimation();
            o.Target = nameof(Visual.Opacity);
            if (from != null && from.HasValue)
                o.InsertKeyFrame(0, from.Value);
            o.InsertKeyFrame(1, to);
            o.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            o.DelayTime = TimeSpan.FromMilliseconds(delayMs);
            o.Duration = TimeSpan.FromMilliseconds(durationMs);
            return o;
        }

        public static ExpressionAnimation StartCentering(Visual v)
        {
            var e = v.Compositor.CreateExpressionAnimation();
            e.Target = nameof(Visual.CenterPoint);
            e.Expression = CENTRE_EXPRESSION;
            v.StartAnimationGroup(e);
            return e;
        }

        public static void PlayScaleEntrance(FrameworkElement target, float from, float to)
        {
            if (!UISettings.AnimationsEnabled)
                return;

            Visual v = target.GetElementVisual();

            if (target.Tag == null)
            {
                StartCentering(v);
                target.Tag = target;
            }

            var e = v.Compositor.CreateEntranceEasingFunction();

            var t = v.Compositor.CreateVector3KeyFrameAnimation();
            t.Target = nameof(Visual.Scale);
            t.InsertKeyFrame(0, new Vector3(from, from, 0));
            t.InsertKeyFrame(1, new Vector3(to, to, 0), e);
            t.Duration = TimeSpan.FromSeconds(0.6);

            var o = CreateFade(v.Compositor, 1, 0, 200);

            var g = v.Compositor.CreateAnimationGroup(t, o);
            v.StartAnimationGroup(g);
        }

        public static void SetStandardReposition(object sender, RoutedEventArgs args)
        {
            if (!UISettings.AnimationsEnabled)
                return;

            UIElement e = (UIElement)sender;
            Visual v = e.GetElementVisual();

            if (!_defaultOffsetAnimations.TryGetValue(v.Compositor, out Vector3KeyFrameAnimation value))
            {
                var o = v.Compositor.CreateVector3KeyFrameAnimation();
                o.Target = nameof(Visual.Offset);
                o.InsertExpressionKeyFrame(0, STARTING_VALUE);
                o.InsertExpressionKeyFrame(1, FINAL_VALUE);
                o.Duration = TimeSpan.FromSeconds(DefaultOffsetDuration);
                _defaultOffsetAnimations[v.Compositor] = o;
                value = o;
            }

            v.SetImplicitAnimation(nameof(Visual.Offset), value);
        }

        public static void SetDropInOut(FrameworkElement background, IList<FrameworkElement> children, FrameworkElement container = null)
        {
            if (!UISettings.AnimationsEnabled)
                return;

            double delay = 0.15;

            var bv = background.EnableTranslation(true).GetElementVisual();
            var ease = bv.Compositor.CreateEntranceEasingFunction();

            var bt = bv.Compositor.CreateVector3KeyFrameAnimation();
            bt.Target = TRANSLATION;
            bt.InsertExpressionKeyFrame(0, "Vector3(0, -this.Target.Size.Y, 0)");
            bt.InsertKeyFrame(1, Vector3.Zero, ease);
            bt.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            bt.DelayTime = TimeSpan.FromSeconds(delay);
            bt.Duration = TimeSpan.FromSeconds(0.7);
            background.SetShowAnimation(bt);

            delay += 0.15;

            foreach (var child in children)
            {
                var v = child.EnableTranslation(true).GetElementVisual();
                var t = v.Compositor.CreateVector3KeyFrameAnimation();
                t.Target = TRANSLATION;
                t.InsertExpressionKeyFrame(0, "Vector3(0, -this.Target.Size.Y, 0)");
                t.InsertKeyFrame(1, Vector3.Zero, ease);
                t.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
                t.DelayTime = TimeSpan.FromSeconds(delay);
                t.Duration = TimeSpan.FromSeconds(0.7);
                child.SetShowAnimation(t);
                delay += 0.075;
            }

            if (container != null)
            {
                var c = container.GetElementVisual();
                var clip = c.Compositor.CreateInsetClip();
                c.Clip = clip;
            }


            // Create hide animation
            var list = new List<FrameworkElement>();
            list.Add(background);
            list.AddRange(children);

            var ht = bv.Compositor.CreateVector3KeyFrameAnimation();
            ht.Target = TRANSLATION;
            ht.InsertExpressionKeyFrame(1, "Vector3(0, -this.Target.Size.Y, 0)", ease);
            ht.Duration = TimeSpan.FromSeconds(0.5);

            foreach (var child in list)
                child.SetHideAnimation(ht);
        }

        public static void SetStandardFadeInOut(object sender, RoutedEventArgs args)
        {
            if (!UISettings.AnimationsEnabled)
                return;

            if (sender is FrameworkElement e)
                SetFadeInOut(e, 200);
        }

        private static void SetFadeInOut(FrameworkElement e, int durationMs)
        {
            var v = e.GetElementVisual();
            e.SetHideAnimation(CreateFade(v.Compositor, 0, null, durationMs));
            e.SetShowAnimation(CreateFade(v.Compositor, 1, null, durationMs));
        }

        public static void StartStartUpAnimation(
            List<FrameworkElement> barElements,
            List<UIElement> contentElements)
        {
            if (!UISettings.AnimationsEnabled)
                return;

            TimeSpan duration1 = TimeSpan.FromSeconds(0.7);

            var c = barElements[0].GetElementVisual().Compositor;
            var backOut = c.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.885f), new Vector2(0.25f, 1.125f));

            double delay = 0.1;
            foreach (var element in barElements)
            {
                var t = c.CreateVector3KeyFrameAnimation();
                t.Target = TRANSLATION;
                t.InsertKeyFrame(0, new Vector3(0, -100, 0));
                t.InsertKeyFrame(1, Vector3.Zero, backOut);
                t.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
                t.DelayTime = TimeSpan.FromSeconds(delay);
                t.Duration = duration1;
                delay += 0.055;

                var v = element.EnableTranslation(true).GetElementVisual();
                v.StartAnimationGroup(t);
            }

            PlayEntrance(contentElements, 200);
        }

        public static void SetThemeShadow(UIElement target, float depth, params UIElement[] recievers)
        {
            if (!Utils.Supports1903 || !ResourceHelper.AppSettings.EnableShadows)
                return;

            // Temporarily, we'll also disable shadows if Windows Animations are disabled
            if (!UISettings.AnimationsEnabled)
                return;

            try
            {
                if (!CompositionCapabilities.GetForCurrentView().AreEffectsFast())
                    return;

                target.Translation = new Vector3(0, 0, depth);

                var shadow = new ThemeShadow();
                target.Shadow = shadow;
                foreach (var r in recievers)
                    shadow.Receivers.Add(r);
            }
            catch { }          
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
