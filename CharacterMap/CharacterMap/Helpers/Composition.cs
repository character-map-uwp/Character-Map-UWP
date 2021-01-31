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
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Core.Direct;
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

        private static Dictionary<Compositor, ImplicitAnimationCollection> _defaultRepositionAnimations { get; }
            = new Dictionary<Compositor, ImplicitAnimationCollection>();

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

        public static ImplicitAnimationCollection GetRepositionCollection(Compositor c)
        {
            if (!_defaultRepositionAnimations.TryGetValue(c, out ImplicitAnimationCollection collection))
            {
                var offsetAnimation = c.CreateVector3KeyFrameAnimation();
                offsetAnimation.InsertExpressionKeyFrame(1f, "this.FinalValue");
                offsetAnimation.Duration = TimeSpan.FromSeconds(Composition.DefaultOffsetDuration);
                offsetAnimation.Target = nameof(Visual.Offset);

                var g = c.CreateAnimationGroup();
                g.Add(offsetAnimation);

                var s = c.CreateImplicitAnimationCollection();
                s.Add(nameof(Visual.Offset), g);
                _defaultRepositionAnimations[c] = s;
                return s;
            }

            return collection;
        }

        public static void PokeUIElementZIndex(UIElement e, XamlDirect xamlDirect = null)
        {
            if (xamlDirect != null)
            {
                var o = xamlDirect.GetXamlDirectObject(e);
                var i = xamlDirect.GetInt32Property(o, XamlPropertyIndex.Canvas_ZIndex);
                xamlDirect.SetInt32Property(o, XamlPropertyIndex.Canvas_ZIndex, i + 1);
                xamlDirect.SetInt32Property(o, XamlPropertyIndex.Canvas_ZIndex, i);
            }
            else
            {
                var index = Canvas.GetZIndex(e);
                Canvas.SetZIndex(e, index + 1);
                Canvas.SetZIndex(e, index);
            }
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

        public static void PlayEntrance(UIElement target, int delayMs = 0, int fromOffsetY = 40, int fromOffsetX = 0, int durationMs = 1000)
        {
            if (!UISettings.AnimationsEnabled)
                return;

            var animation = CreateEntranceAnimation(target, new Vector3(fromOffsetX, fromOffsetY, 0), delayMs, durationMs);
            target.GetElementVisual().StartAnimationGroup(animation);
        }

        public static void SetStandardEntrance(FrameworkElement sender, object args)
        {
            if (!UISettings.AnimationsEnabled)
                return;

            if (sender is FrameworkElement e)
                e.SetShowAnimation(CreateEntranceAnimation(e, new Vector3(100, 0, 0), 200));
        }

        public static void PlayStandardEntrance(object sender, RoutedEventArgs args)
        {
            if (!UISettings.AnimationsEnabled)
                return;

            if (sender is FrameworkElement e)
                e.GetElementVisual().StartAnimationGroup(CreateEntranceAnimation(e, new Vector3(100, 0, 0), 200));
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

        public static void PlayEntrance(List<UIElement> targets, int delayMs = 0, int fromOffsetY = 40, int fromOffsetX = 0, int durationMs = 1000, int staggerMs = 83)
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
            o.InsertKeyFrame(1, to, c.CreateEntranceEasingFunction());
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

        public static void PlayFullHeightSlideUpEntrance(FrameworkElement target)
        {
            if (!UISettings.AnimationsEnabled)
                return;

            Visual v = target.EnableTranslation(true).GetElementVisual();

            var t = v.Compositor.CreateVector3KeyFrameAnimation();
            t.Target = TRANSLATION;
            t.InsertExpressionKeyFrame(0, "Vector3(0, this.Target.Size.Y, 0)");
            t.InsertExpressionKeyFrame(1, "Vector3(0, 0, 0)");
            t.Duration = TimeSpan.FromSeconds(DefaultOffsetDuration);
            v.StartAnimationGroup(t);
        }

        public static Vector3KeyFrameAnimation CreateSlideOut(UIElement e, float x, float y)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(e, true);
            Visual v = ElementCompositionPreview.GetElementVisual(e);

            var o = v.Compositor.CreateVector3KeyFrameAnimation();
            o.Target = TRANSLATION;
            o.InsertExpressionKeyFrame(0, "this.StartingValue");
            o.InsertKeyFrame(1, new System.Numerics.Vector3(x ,y, 0));
            o.Duration = TimeSpan.FromSeconds(Composition.DefaultOffsetDuration);

            return o;
        }

        public static Vector3KeyFrameAnimation CreateSlideOutX(UIElement e)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(e, true);
            Visual v = ElementCompositionPreview.GetElementVisual(e);

            var o = v.Compositor.CreateVector3KeyFrameAnimation();
            o.Target = TRANSLATION;
            o.InsertExpressionKeyFrame(0, "this.StartingValue");
            o.InsertExpressionKeyFrame(1, "Vector3(this.Target.Size.X, 0, 0)");
            o.Duration = TimeSpan.FromSeconds(Composition.DefaultOffsetDuration);

            return o;
        }
        public static Vector3KeyFrameAnimation CreateSlideOutY(UIElement e)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(e, true);
            Visual v = ElementCompositionPreview.GetElementVisual(e);

            var o = v.Compositor.CreateVector3KeyFrameAnimation();
            o.Target = TRANSLATION;
            o.InsertExpressionKeyFrame(0, "this.StartingValue");
            o.InsertExpressionKeyFrame(1, "Vector3(0, this.Target.Size.Y, 0)");
            o.Duration = TimeSpan.FromSeconds(Composition.DefaultOffsetDuration);

            return o;
        }

        public static Vector3KeyFrameAnimation CreateSlideIn(UIElement e)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(e, true);
            Visual v = ElementCompositionPreview.GetElementVisual(e);

            var o = v.Compositor.CreateVector3KeyFrameAnimation();
            o.Target = TRANSLATION;
            o.InsertExpressionKeyFrame(1, "Vector3(0, 0, 0)");
            o.Duration = TimeSpan.FromSeconds(Composition.DefaultOffsetDuration);

            return o;
        }


        #region Default Composition Transitions 

        /// <summary>
        /// Creates the detault Forward composition animation
        /// </summary>
        /// <param name="outElement"></param>
        /// <param name="inElement"></param>
        /// <returns></returns>
        public static void StartCompositionExpoZoomForwardTransition(FrameworkElement outElement, FrameworkElement inElement)
        {
            if (!UISettings.AnimationsEnabled)
            {
                return;
            }

            Compositor compositor = ElementCompositionPreview.GetElementVisual(outElement).Compositor;

            Visual outVisual = ElementCompositionPreview.GetElementVisual(outElement);
            Visual inVisual = ElementCompositionPreview.GetElementVisual(inElement);

            CompositionAnimationGroup outgroup = compositor.CreateAnimationGroup();
            CompositionAnimationGroup ingroup = compositor.CreateAnimationGroup();

            TimeSpan outDuration = TimeSpan.FromSeconds(0.3);
            TimeSpan inStart = TimeSpan.FromSeconds(0.25);
            TimeSpan inDuration = TimeSpan.FromSeconds(0.6);

            CubicBezierEasingFunction ease = compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.95f, 0.05f),
                new Vector2(0.79f, 0.04f));

            CubicBezierEasingFunction easeOut = compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.13f, 1.0f),
                new Vector2(0.49f, 1.0f));

            // OUT ELEMENT
            {
                outVisual.CenterPoint = outVisual.Size.X > 0
                   ? new Vector3(outVisual.Size / 2f, 0f)
                   : new Vector3((float)Window.Current.Bounds.Width / 2f, (float)Window.Current.Bounds.Height / 2f, 0f);

                // SCALE OUT
                var sout = compositor.CreateVector3KeyFrameAnimation();
                sout.InsertKeyFrame(1, new Vector3(1.3f, 1.3f, 1f), ease);
                sout.Duration = outDuration;
                sout.Target = nameof(outVisual.Scale);

                // FADE OUT
                var oout = compositor.CreateScalarKeyFrameAnimation();
                oout.InsertKeyFrame(1, 0f, ease);
                oout.Duration = outDuration;
                oout.Target = nameof(outVisual.Opacity);
            }

            // IN ELEMENT
            {
                inVisual.CenterPoint = inVisual.Size.X > 0
                      ? new Vector3(inVisual.Size / 2f, 0f)
                      : new Vector3(outVisual.Size / 2f, 0f);


                // SCALE IN
                var sO = inVisual.Compositor.CreateVector3KeyFrameAnimation();
                sO.Duration = inDuration;
                sO.Target = nameof(inVisual.Scale);
                sO.InsertKeyFrame(0, new Vector3(0.7f, 0.7f, 1.0f), easeOut);
                sO.InsertKeyFrame(1, new Vector3(1.0f, 1.0f, 1.0f), easeOut);
                sO.DelayTime = inStart;
                ingroup.Add(sO);

                // FADE IN
                inVisual.Opacity = 0f;
                var op = inVisual.Compositor.CreateScalarKeyFrameAnimation();
                op.DelayTime = inStart;
                op.Duration = inDuration;
                op.Target = nameof(outVisual.Opacity);
                op.InsertKeyFrame(1, 0f, easeOut);
                op.InsertKeyFrame(1, 1f, easeOut);
                ingroup.Add(op);

            }

            outVisual.StartAnimationGroup(outgroup);
            inVisual.StartAnimationGroup(ingroup);
        }

        /// <summary>
        /// Creates the default backwards composition animation
        /// </summary>
        /// <param name="outElement"></param>
        /// <param name="inElement"></param>
        /// <returns></returns>
        //CompositionStoryboard CreateCompositionExpoZoomBackward(FrameworkElement outElement, FrameworkElement inElement)
        //{
        //    Compositor compositor = ElementCompositionPreview.GetElementVisual(outElement).Compositor;

        //    Visual outVisual = ElementCompositionPreview.GetElementVisual(outElement);
        //    Visual inVisual = ElementCompositionPreview.GetElementVisual(inElement);

        //    CompositionAnimationGroup outgroup = compositor.CreateAnimationGroup();
        //    CompositionAnimationGroup ingroup = compositor.CreateAnimationGroup();

        //    TimeSpan outDuration = TimeSpan.FromSeconds(0.3);
        //    TimeSpan inDuration = TimeSpan.FromSeconds(0.4);

        //    CubicBezierEasingFunction ease = compositor.CreateCubicBezierEasingFunction(
        //        new Vector2(0.95f, 0.05f),
        //        new Vector2(0.79f, 0.04f));

        //    CubicBezierEasingFunction easeOut = compositor.CreateCubicBezierEasingFunction(
        //        new Vector2(0.19f, 1.0f),
        //        new Vector2(0.22f, 1.0f));


        //    // OUT ELEMENT
        //    {
        //        outVisual.CenterPoint = outVisual.Size.X > 0
        //            ? new Vector3(outVisual.Size / 2f, 0f)
        //            : new Vector3((float)this.ActualWidth / 2f, (float)this.ActualHeight / 2f, 0f);

        //        // SCALE OUT
        //        var sO = compositor.CreateVector3KeyFrameAnimation();
        //        sO.Duration = outDuration;
        //        sO.Target = nameof(outVisual.Scale);
        //        sO.InsertKeyFrame(1, new Vector3(0.7f, 0.7f, 1.0f), ease);
        //        outgroup.Add(sO);

        //        // FADE OUT
        //        var op = compositor.CreateScalarKeyFrameAnimation();
        //        op.Duration = outDuration;
        //        op.Target = nameof(outVisual.Opacity);
        //        op.InsertKeyFrame(1, 0f, ease);
        //        outgroup.Add(op);
        //    }

        //    // IN ELEMENT
        //    {
        //        inVisual.CenterPoint = inVisual.Size.X > 0
        //             ? new Vector3(inVisual.Size / 2f, 0f)
        //             : new Vector3((float)this.ActualWidth / 2f, (float)this.ActualHeight / 2f, 0f);


        //        // SCALE IN
        //        ingroup.Add(
        //            inVisual.CreateVector3KeyFrameAnimation(nameof(Visual.Scale))
        //                .AddScaleKeyFrame(0, 1.3f)
        //                .AddScaleKeyFrame(1, 1f, easeOut)
        //                .SetDuration(inDuration)
        //                .SetDelayTime(outDuration)
        //                .SetDelayBehavior(AnimationDelayBehavior.SetInitialValueBeforeDelay));

        //        // FADE IN
        //        inVisual.Opacity = 0f;
        //        var op = inVisual.Compositor.CreateScalarKeyFrameAnimation();
        //        op.DelayTime = outDuration;
        //        op.Duration = inDuration;
        //        op.Target = nameof(outVisual.Opacity);
        //        op.InsertKeyFrame(1, 0f, easeOut);
        //        op.InsertKeyFrame(1, 1f, easeOut);
        //        ingroup.Add(op);

        //    }

        //    CompositionStoryboard group = new CompositionStoryboard();
        //    group.Add(new CompositionTimeline(outVisual, outgroup, ease));
        //    group.Add(new CompositionTimeline(inVisual, ingroup, easeOut));
        //    return group;
        //}

        #endregion

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
