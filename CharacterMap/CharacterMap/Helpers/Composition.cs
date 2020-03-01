using CharacterMap.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Helpers
{
    public static class Composition
    {
        public const double DefaultOffsetDuration = 0.325;

        private static Dictionary<Compositor, Vector3KeyFrameAnimation> _defaultOffsetAnimations { get; } 
            = new Dictionary<Compositor, Vector3KeyFrameAnimation>();

        private static string CENTRE_EXPRESSION(float x, float y) =>
            $"({nameof(Vector3)}(this.Target.{nameof(Visual.Size)}.{nameof(Vector2.X)} * {x}f, " +
            $"this.Target.{nameof(Visual.Size)}.{nameof(Vector2.Y)} * {y}f, 0f))";

        public static void PlayEntrance(UIElement target, int delayMs = 0, int fromOffset = 140)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(target, true);
            Visual v = ElementCompositionPreview.GetElementVisual(target);

            TimeSpan delay = TimeSpan.FromMilliseconds(delayMs);
            var e = v.Compositor.CreateCubicBezierEasingFunction(new Vector2(.1f, .9f), new Vector2(.2f, 1));

            var t = v.Compositor.CreateVector3KeyFrameAnimation();
            t.Target = "Translation";
            t.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            t.DelayTime = delay;
            t.InsertKeyFrame(0, new Vector3(0, fromOffset, 0));
            t.InsertKeyFrame(1, new Vector3(0), e);
            t.Duration = TimeSpan.FromSeconds(1);

            var o = v.Compositor.CreateScalarKeyFrameAnimation();
            o.Target = nameof(Visual.Opacity);
            o.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            t.DelayTime = delay;
            o.InsertKeyFrame(0, 0);
            o.InsertKeyFrame(1, 1);
            o.Duration = TimeSpan.FromSeconds(0.3);

            var g = v.Compositor.CreateAnimationGroup();
            g.Add(t);
            g.Add(o);

            v.StartAnimationGroup(g);
        }

        public static void PlayScaleEntrance(FrameworkElement target, float from, float to)
        {
            Visual v = ElementCompositionPreview.GetElementVisual(target);


            if (target.Tag == null)
            {
                var c = v.Compositor.CreateExpressionAnimation();
                c.Target = nameof(Visual.CenterPoint);
                c.Expression = CENTRE_EXPRESSION(.5f, .5f);
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
            Visual v = ElementCompositionPreview.GetElementVisual(e);

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

            var set = v.Compositor.CreateImplicitAnimationCollection();
            set.Add(nameof(Visual.Offset), value);
            v.ImplicitAnimations = set;
        }

        public static void SetThemeShadow(UIElement target, float depth, params UIElement[] recievers)
        {
            if (!Utils.Supports1903)
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
