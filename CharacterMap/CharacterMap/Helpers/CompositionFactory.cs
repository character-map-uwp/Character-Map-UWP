using CharacterMap.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
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
    public class CompositionFactory : DependencyObject
    {
        public const double DefaultOffsetDuration = 0.325;

        public static bool AnimationEnabled { get; set; }

        public static UISettings UISettings { get; }

        private static string CENTRE_EXPRESSION =>
            $"({nameof(Vector3)}(this.Target.{nameof(Visual.Size)}.{nameof(Vector2.X)} * {{0}}f, " +
            $"this.Target.{nameof(Visual.Size)}.{nameof(Vector2.Y)} * {{1}}f, 0f))";

        public const string TRANSLATION = "Translation";
        public const string STARTING_VALUE = "this.StartingValue";
        public const string FINAL_VALUE = "this.FinalValue";
        public const int DEFAULT_STAGGER_MS = 83;

        #region Attached Properties

        public static bool GetEnableBounceScale(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableBounceScaleProperty);
        }

        public static void SetEnableBounceScale(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableBounceScaleProperty, value);
        }

        public static readonly DependencyProperty EnableBounceScaleProperty =
            DependencyProperty.RegisterAttached("EnableBounceScale", typeof(bool), typeof(CompositionFactory), new PropertyMetadata(false, (d,e) =>
            {
                if (d is FrameworkElement f)
                {
                    Visual v = f.GetElementVisual();
                    if (e.NewValue is bool b && b)
                    {
                        CompositionFactory.EnableStandardTranslation(v, 0.15);
                    }
                    else
                    {
                        v.Properties.SetImplicitAnimation(CompositionFactory.TRANSLATION, null);
                    }
                }
            }));

        public static Duration GetOpacityDuration(DependencyObject obj)
        {
            return (Duration)obj.GetValue(OpacityDurationProperty);
        }

        public static void SetOpacityDuration(DependencyObject obj, Duration value)
        {
            obj.SetValue(OpacityDurationProperty, value);
        }

        public static readonly DependencyProperty OpacityDurationProperty =
            DependencyProperty.RegisterAttached("OpacityDuration", typeof(Duration), typeof(CompositionFactory), new PropertyMetadata(new Duration(TimeSpan.FromSeconds(0)), (d, e) =>
            {
                if (d is FrameworkElement element && e.NewValue is Duration t)
                {
                    SetOpacityTransition(element, t.HasTimeSpan ? t.TimeSpan : TimeSpan.Zero);
                }
            }));

        public static double GetCornerRadius(DependencyObject obj)
        {
            return (double)obj.GetValue(CornerRadiusProperty);
        }

        public static void SetCornerRadius(DependencyObject obj, double value)
        {
            obj.SetValue(CornerRadiusProperty, value);
        }

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.RegisterAttached("CornerRadius", typeof(double), typeof(CompositionFactory), new PropertyMetadata(0d, (d, e) =>
            {
                if (d is FrameworkElement element && e.NewValue is double v)
                {
                    SetCornerRadius(element, (float)v);
                }
            }));

        #endregion

        static CompositionFactory()
        {
            UISettings = new UISettings();
        }

        public static ImplicitAnimationCollection GetRepositionCollection(Compositor c)
        {
            return c.GetCached("RepoColl", () =>
            {
                var g = c.CreateAnimationGroup();
                g.Add(c.CreateVector3KeyFrameAnimation()
                            .SetTarget(nameof(Visual.Offset))
                            .AddKeyFrame(1f, "this.FinalValue")
                            .SetDuration(CompositionFactory.DefaultOffsetDuration));

                var s = c.CreateImplicitAnimationCollection();
                s.Add(nameof(Visual.Offset), g);
                return s;
            });
        }

        public static ICompositionAnimationBase CreateScaleAnimation(Compositor c)
        {
            return c.GetCached("ScaleAni", () =>
            {
                return c.CreateVector3KeyFrameAnimation()
                    .AddKeyFrame(1f, "this.FinalValue")
                    .SetDuration(CompositionFactory.DefaultOffsetDuration)
                    .SetTarget(nameof(Visual.Scale));
            });
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
            if (!UISettings.AnimationsEnabled)
                return;

            Visual v = e.EnableTranslation(true).GetElementVisual();

            var g = v.GetCached("OPA", () =>
            {
                var t = v.CreateVector3KeyFrameAnimation(CompositionFactory.TRANSLATION)
                        .AddKeyFrame(1, 0, 200)
                        .SetDuration(0.375);

                var o = CompositionFactory.CreateFade(v.Compositor, 0, null, 200);
                return v.Compositor.CreateAnimationGroup(t, o);
            });

            e.SetHideAnimation(g);
            e.SetShowAnimation(CompositionFactory.CreateEntranceAnimation(e, new Vector3(0, 200, 0), 0, 550));
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
            string key = $"CEA{from.X}{from.Y}{delayMs}{durationMs}";
            Compositor c = target.EnableTranslation(true).GetElementVisual().Compositor;

            return c.GetCached(key, () =>
            {
                TimeSpan delay = TimeSpan.FromMilliseconds(delayMs);
                var e = c.GetCachedEntranceEase();
                var t = c.CreateVector3KeyFrameAnimation()
                    .SetTarget(TRANSLATION)
                    .SetDelayBehavior(AnimationDelayBehavior.SetInitialValueBeforeDelay)
                    .SetDelayTime(delay)
                    .AddKeyFrame(0, from)
                    .AddKeyFrame(1, 0, e)
                    .SetDuration(TimeSpan.FromMilliseconds(durationMs));

                var o = CreateFade(c, 1, 0, (int)(durationMs * 0.33), delayMs);
                return c.CreateAnimationGroup(t, o);
            });
        }

        public static void SetCornerRadius(UIElement target, float size)
        {
            var vis = target.GetElementVisual();
            var rec = vis.Compositor.CreateRoundedRectangleGeometry();
            rec.CornerRadius = new(size);
            rec.LinkShapeSize(vis);
            var clip = vis.Compositor.CreateGeometricClip(rec);
            vis.Clip = clip;
        }

        public static void PlayEntrance(List<UIElement> targets, int delayMs = 0, int fromOffsetY = 40, int fromOffsetX = 0, int durationMs = 1000, int staggerMs = 83)
        {
            if (!UISettings.AnimationsEnabled)
                return;

            int start = delayMs;

            foreach (var target in targets)
            {
                if (target is null) continue;
                var animation = CreateEntranceAnimation(target, new Vector3(fromOffsetX, fromOffsetY, 0), start, durationMs);
                target.GetElementVisual().StartAnimationGroup(animation);
                start += staggerMs;
            }
        }

        public static CompositionAnimation CreateFade(Compositor c, float to, float? from, int durationMs, int delayMs = 0)
        {
            string key = $"SFade{to}{from}{durationMs}{delayMs}";
            return c.GetCached(key, () =>
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
            });
        }

        public static ExpressionAnimation StartCentering(Visual v, float x = 0.5f, float y = 0.5f)
        {
            v.StopAnimation(nameof(Visual.CenterPoint));

            var e = v.GetCached($"CP{x}{y}",
                        () => v.CreateExpressionAnimation(nameof(Visual.CenterPoint))
                                .SetExpression(string.Format(
                                    CENTRE_EXPRESSION, 
                                    x.ToString(CultureInfo.InvariantCulture.NumberFormat), 
                                    y.ToString(CultureInfo.InvariantCulture.NumberFormat))));

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

            var t = v.CreateVector3KeyFrameAnimation(nameof(Visual.Scale))
                .AddKeyFrame(0, new Vector3(from, from, 0))
                .AddKeyFrame(1, new Vector3(to, to, 0), e)
                .SetDuration(0.6);

            var o = CreateFade(v.Compositor, 1, 0, 200);

            var g = v.Compositor.CreateAnimationGroup(t, o);
            v.StartAnimationGroup(g);
        }

        public static void SetStandardReposition(UIElement e)
        {
            if (!UISettings.AnimationsEnabled)
                return;

            Visual v = e.GetElementVisual();

            var value = v.GetCached("DefaultOffsetAnimation",
                            () => v.CreateVector3KeyFrameAnimation(nameof(Visual.Offset))
                                    .AddKeyFrame(0, STARTING_VALUE)
                                    .AddKeyFrame(1, FINAL_VALUE)
                                    .SetDuration(DefaultOffsetDuration));

            v.SetImplicitAnimation(nameof(Visual.Offset), value);
        }

        public static void DisableStandardReposition(FrameworkElement f)
        {
            f.GetElementVisual().ImplicitAnimations?.Remove(nameof(Visual.Offset));
        }

        public static Visual EnableStandardTranslation(Visual v, double? duration = null)
        {
            if (!UISettings.AnimationsEnabled)
                return v;

            var o = v.GetCached($"__ST{(duration.HasValue ? duration.Value : DefaultOffsetDuration)}",
                () => v.CreateVector3KeyFrameAnimation(CompositionFactory.TRANSLATION)
                       .AddKeyFrame(0, STARTING_VALUE)
                       .AddKeyFrame(1, FINAL_VALUE)
                       .SetDuration(duration ?? DefaultOffsetDuration));

            v.Properties.SetImplicitAnimation(CompositionFactory.TRANSLATION, o);
            return v;
        }

        public static void SetDropInOut(FrameworkElement background, IList<FrameworkElement> children, FrameworkElement container = null)
        {
            if (background is null || children.Count == 0)
                return;

            if (ResourceHelper.AllowAnimation is false)
            {
                background.SetShowAnimation(null);
                background.SetHideAnimation(null);
                foreach (var child in children)
                {
                    child.SetShowAnimation(null);
                    child.SetHideAnimation(null);
                }

                return;
            }

            double delay = 0.15;

            var bv = background.EnableTranslation(true).GetElementVisual();
            var ease = bv.Compositor.GetCachedEntranceEase();

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
                var t = c.CreateVector3KeyFrameAnimation()
                    .SetTarget(TRANSLATION)
                    .AddKeyFrame(0, 0, -100)
                    .AddKeyFrame(1, 0, backOut)
                    .SetDelayBehavior(AnimationDelayBehavior.SetInitialValueBeforeDelay)
                    .SetDelayTime(TimeSpan.FromSeconds(delay))
                    .SetDuration(duration1);
               
                delay += 0.055;

                var v = element.EnableTranslation(true).GetElementVisual();
                v.StartAnimationGroup(t);
            }

            PlayEntrance(contentElements, 200);
        }

        public static void SetThemeShadow(UIElement target, float depth, params UIElement[] recievers)
        {
            if (!Utils.Supports1903 || !ResourceHelper.AppSettings.EnableShadows || ResourceHelper.SupportsShadows() is false)
                return;

            // Temporarily, we'll also disable shadows if Windows Animations are disabled
            if (!UISettings.AnimationsEnabled)
                return;

            try
            {
                if (!CompositionCapabilities.GetForCurrentView().AreEffectsFast())
                    return;

                target.Translation = new Vector3(0, 0, depth);

                ThemeShadow shadow = new ();
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
            var t = v.GetCached("_FHSU", () =>
                v.CreateVector3KeyFrameAnimation(TRANSLATION)
                    .AddKeyFrame(0, "Vector3(0, this.Target.Size.Y, 0)")
                    .AddKeyFrame(1, "Vector3(0, 0, 0)")
                    .SetDuration(DefaultOffsetDuration));

            v.StartAnimationGroup(t);
        }

        public static Vector3KeyFrameAnimation CreateSlideOut(UIElement e, float x, float y)
        {
            Visual v = e.EnableTranslation(true).GetElementVisual();
            return v.GetCached("_SLDO",
                    () => v.CreateVector3KeyFrameAnimation(TRANSLATION)
                            .AddKeyFrame(0, STARTING_VALUE)
                            .AddKeyFrame(1, x, y, 0)
                            .SetDuration(DefaultOffsetDuration));
        }

        public static Vector3KeyFrameAnimation CreateSlideOutX(UIElement e)
        {
            Visual v = e.EnableTranslation(true).GetElementVisual();
            return v.GetCached("SOX",
                    () => v.CreateVector3KeyFrameAnimation(TRANSLATION)
                            .AddKeyFrame(0, STARTING_VALUE)
                            .AddKeyFrame(1, "Vector3(this.Target.Size.X, 0, 0)")
                            .SetDuration(DefaultOffsetDuration));
        }

        public static Vector3KeyFrameAnimation CreateSlideOutY(UIElement e)
        {
            Visual v = e.EnableTranslation(true).GetElementVisual();
            return v.GetCached("SOY",
                    () => v.CreateVector3KeyFrameAnimation(TRANSLATION)
                            .AddKeyFrame(0, STARTING_VALUE)
                            .AddKeyFrame(1, "Vector3(0, this.Target.Size.Y, 0)")
                            .SetDuration(DefaultOffsetDuration));
        }

        public static Vector3KeyFrameAnimation CreateSlideIn(UIElement e)
        {
            Visual v = e.EnableTranslation(true).GetElementVisual();
            return v.GetCached("_SLDI",
                    () => v.CreateVector3KeyFrameAnimation(TRANSLATION)
                            .AddKeyFrame(1, Vector3.Zero)
                            .SetDuration(DefaultOffsetDuration));
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
                return;

            Compositor compositor = ElementCompositionPreview.GetElementVisual(outElement).Compositor;

            Visual outVisual = ElementCompositionPreview.GetElementVisual(outElement);
            Visual inVisual = ElementCompositionPreview.GetElementVisual(inElement);

            CompositionAnimationGroup outgroup = compositor.CreateAnimationGroup();
            CompositionAnimationGroup ingroup = compositor.CreateAnimationGroup();

            TimeSpan outDuration = TimeSpan.FromSeconds(0.3);
            TimeSpan inStart = TimeSpan.FromSeconds(0.25);
            TimeSpan inDuration = TimeSpan.FromSeconds(0.6);

            CubicBezierEasingFunction ease = compositor.GetCached("ExpoZoomEase", 
                () => compositor.CreateCubicBezierEasingFunction(0.95f, 0.05f, 0.79f, 0.04f));

            CubicBezierEasingFunction easeOut = compositor.GetCached("ExpoZoomOutEase", 
                () => compositor.CreateCubicBezierEasingFunction(0.13f, 1.0f, 0.49f, 1.0f));

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
