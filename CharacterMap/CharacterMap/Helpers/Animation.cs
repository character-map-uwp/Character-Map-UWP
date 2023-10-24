using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Media3D;

namespace CharacterMap.Helpers
{
    public enum AnimationTargetType
    {
        Undefined = 0,
        RootContainer = 1,
        Child = 2
    }

    /// <summary>
    /// A collection of useful animation-focused extension methods
    /// </summary>
    [Bindable]
    public static class Animation
    {
        #region Composite Transform

        public static CompositeTransform GetNewCompositeTransform(this FrameworkElement element, bool centreOriginOnCreation = true, bool overwriteOtherTransforms = true)
        {
            element.RenderTransform = null;
            return element.GetCompositeTransform(centreOriginOnCreation, overwriteOtherTransforms);
        }

        public static CompositeTransform GetCompositeTransform(this FrameworkElement element, bool centreOriginOnCreation = true, bool overwriteOtherTransforms = true)
        {
            CompositeTransform ct = null;

            ct = element.RenderTransform as CompositeTransform;

            if (ct != null)
                return ct;

            // 3. If there's nothing there, create a new CompositeTransform
            if (ct == null && element.RenderTransform == null)
            {
                element.RenderTransform = new CompositeTransform();
                ct = (CompositeTransform)element.RenderTransform;
                if (centreOriginOnCreation)
                {
                    ct.CenterX = ct.CenterY = 0.5;
                    element.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                }
            }
            else
            {
                ct = new CompositeTransform();
                if (centreOriginOnCreation)
                {
                    ct.CenterX = ct.CenterY = 0.5;
                    element.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                }

                // 5. See if the existing item is a singular transform, and convert it to a CompositeTransform
                if (element.RenderTransform is Transform transform)
                {
                    ApplyTransform(ref ct, transform);
                    element.RenderTransform = ct;
                }
                else
                {
                    // 6. If we're a group of transforms, convert each child individually
                    if (element.RenderTransform is TransformGroup group)
                    {
                        foreach (var tran in group.Children)
                            ApplyTransform(ref ct, tran);

                        element.RenderTransform = ct;
                    }
                }
            }
            return ct;
        }

        /// <summary>
        /// Adds the effect of a regular transform to a composite transform
        /// </summary>
        /// <param name="ct"></param>
        /// <param name="t"></param>
        internal static void ApplyTransform(ref CompositeTransform ct, Transform t)
        {
            if (t is TranslateTransform tt)
            {
                ct.TranslateX = tt.X;
                ct.TranslateY = tt.Y;
            }
            else if (t is RotateTransform rt)
            {
                ct.Rotation = rt.Angle;
                ct.CenterX = rt.CenterX;
                ct.CenterY = rt.CenterY;
            }
            else if (t is SkewTransform sK)
            {
                ct.SkewX = sK.AngleX;
                ct.SkewY = sK.AngleY;
                ct.CenterX = sK.CenterX;
                ct.CenterY = sK.CenterY;
            }
            else if (t is ScaleTransform sc)
            {
                ct.ScaleX = sc.ScaleX;
                ct.ScaleY = sc.ScaleY;
                ct.CenterX = sc.CenterX;
                ct.CenterY = sc.CenterY;
            }
        }

        #endregion

        #region Plane Projection

        /// <summary>
        /// Gets the plane projection from a FrameworkElement's projection property. If 
        /// the property is null or not a plane projection, a new plane projection is created
        /// and set as the plane projection and then returned
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public static PlaneProjection GetPlaneProjection(this FrameworkElement element)
        {
            PlaneProjection projection = null;

            projection = element.Projection as PlaneProjection;
            if (projection == null)
            {
                element.Projection = new PlaneProjection();
                projection = (PlaneProjection)element.Projection;
            }

            return projection;
        }


        #endregion

        #region Storyboard

        /// <summary>
        /// Returns an await-able task that runs the storyboard through to completion
        /// </summary>
        /// <param name="storyboard"></param>
        /// <returns></returns>
        public static Task BeginAsync(this Storyboard storyboard)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            if (storyboard == null) tcs.SetException(new ArgumentNullException());
            else
            {
                void onComplete(object s, object e)
                {
                    storyboard.Completed -= onComplete;
                    tcs.SetResult(true);
                }

                storyboard.Completed += onComplete;
                storyboard.Begin();
            }
            return tcs.Task;
        }

        public static void AddTimeline(this Storyboard storyboard, Timeline timeline, DependencyObject target, String targetProperty)
        {
            if (target is FrameworkElement frameworkElement)
            {
                if (targetProperty.StartsWith(TargetProperty.CompositeTransform.Identifier))
                    GetCompositeTransform(frameworkElement);
                else if (targetProperty.StartsWith(TargetProperty.PlaneProjection.Identifier))
                    GetPlaneProjection(frameworkElement);
                else if (targetProperty.StartsWith(TargetProperty.CompositeTransform3D.Identifier))
                    GetCompositeTransform3D(frameworkElement);
            }

            Storyboard.SetTarget(timeline, target);
            Storyboard.SetTargetProperty(timeline, targetProperty);

            storyboard.Children.Add(timeline);
        }

        #endregion

        #region Timelines

        public static DoubleAnimationUsingKeyFrames AddDiscreteKeyFrame(this DoubleAnimationUsingKeyFrames doubleAnimation, double seconds, double value)
        {
            doubleAnimation.AddDiscreteKeyFrame(TimeSpan.FromSeconds(seconds), value);
            return doubleAnimation;
        }

        public static DoubleAnimationUsingKeyFrames AddDiscreteKeyFrame(this DoubleAnimationUsingKeyFrames doubleAnimation, TimeSpan time, double value)
        {
            doubleAnimation.KeyFrames.Add(new DiscreteDoubleKeyFrame
            {
                KeyTime = KeyTime.FromTimeSpan(time),
                Value = value
            });

            return doubleAnimation;
        }

        /// <summary>
        /// Adds a <see cref="LinearDoubleKeyFrame"/>
        /// </summary>
        /// <param name="doubleAnimation"></param>
        /// <param name="seconds">Duration in seconds</param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static DoubleAnimationUsingKeyFrames AddKeyFrame(this DoubleAnimationUsingKeyFrames doubleAnimation, double seconds, double value)
        {
            doubleAnimation.AddLinearDoubleKeyFrame(TimeSpan.FromSeconds(seconds), value);
            return doubleAnimation;
        }

        /// <summary>
        /// Adds a <see cref="LinearDoubleKeyFrame"/>
        /// </summary>
        public static DoubleAnimationUsingKeyFrames AddKeyFrame(this DoubleAnimationUsingKeyFrames doubleAnimation, TimeSpan keyTime, double value)
        {
            doubleAnimation.AddLinearDoubleKeyFrame(keyTime, value);
            return doubleAnimation;
        }

        /// <summary>
        /// Adds a <see cref="SplineDoubleKeyFrame"/>
        /// </summary>
        /// <param name="doubleAnimation"></param>
        /// <param name="seconds">Duration in seconds</param>
        /// <param name="value"></param>
        /// <param name="spline"></param>
        /// <returns></returns>
        public static DoubleAnimationUsingKeyFrames AddKeyFrame(this DoubleAnimationUsingKeyFrames doubleAnimation, double seconds, double value, KeySpline spline)
        {
            doubleAnimation.AddSplineDoubleKeyFrame(TimeSpan.FromSeconds(seconds), value, spline);
            return doubleAnimation;
        }

        /// <summary>
        /// Adds a <see cref="SplineDoubleKeyFrame"/>
        /// </summary>
        public static DoubleAnimationUsingKeyFrames AddKeyFrame(this DoubleAnimationUsingKeyFrames doubleAnimation, TimeSpan keyTime, double value, KeySpline spline)
        {
            doubleAnimation.AddSplineDoubleKeyFrame(keyTime, value, spline);
            return doubleAnimation;
        }

        /// <summary>
        /// Adds an <see cref="EasingDoubleKeyFrame"/>
        /// </summary>
        /// <param name="doubleAnimation"></param>
        /// <param name="seconds">Duration in seconds</param>
        /// <param name="value"></param>
        /// <param name="ease"></param>
        /// <returns></returns>
        public static DoubleAnimationUsingKeyFrames AddKeyFrame(this DoubleAnimationUsingKeyFrames doubleAnimation, double seconds, double value, EasingFunctionBase ease = null)
        {
            doubleAnimation.AddEasingDoubleKeyFrame(TimeSpan.FromSeconds(seconds), value, ease);
            return doubleAnimation;
        }

        /// <summary>
        /// Adds an <see cref="EasingDoubleKeyFrame"/>
        /// </summary>
        public static DoubleAnimationUsingKeyFrames AddKeyFrame(this DoubleAnimationUsingKeyFrames doubleAnimation, TimeSpan keyTime, double value, EasingFunctionBase ease = null)
        {
            doubleAnimation.AddEasingDoubleKeyFrame(keyTime, value, ease);
            return doubleAnimation;
        }



        private static DoubleAnimationUsingKeyFrames AddLinearDoubleKeyFrame(this DoubleAnimationUsingKeyFrames doubleAnimation, double seconds, double value)
        {
            doubleAnimation.AddEasingDoubleKeyFrame(TimeSpan.FromSeconds(seconds), value);
            return doubleAnimation;
        }

        private static DoubleAnimationUsingKeyFrames AddLinearDoubleKeyFrame(this DoubleAnimationUsingKeyFrames doubleAnimation, TimeSpan time, double value)
        {
            doubleAnimation.KeyFrames.Add(new LinearDoubleKeyFrame
            {
                KeyTime = KeyTime.FromTimeSpan(time),
                Value = value
            });

            return doubleAnimation;
        }

        private static DoubleAnimationUsingKeyFrames AddEasingDoubleKeyFrame(this DoubleAnimationUsingKeyFrames doubleAnimation, double seconds, double value, EasingFunctionBase ease = null)
        {
            doubleAnimation.AddEasingDoubleKeyFrame(TimeSpan.FromSeconds(seconds), value, ease);
            return doubleAnimation;
        }

        private static DoubleAnimationUsingKeyFrames AddEasingDoubleKeyFrame(this DoubleAnimationUsingKeyFrames doubleAnimation, TimeSpan time, double value, EasingFunctionBase ease = null)
        {
            doubleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame
            {
                KeyTime = KeyTime.FromTimeSpan(time),
                Value = value,
                EasingFunction = ease
            });

            return doubleAnimation;
        }

        private static DoubleAnimationUsingKeyFrames AddSplineDoubleKeyFrame(this DoubleAnimationUsingKeyFrames doubleAnimation, double seconds, double value, KeySpline spline = null)
        {
            doubleAnimation.AddSplineDoubleKeyFrame(TimeSpan.FromSeconds(seconds), value, spline);
            return doubleAnimation;
        }

        private static DoubleAnimationUsingKeyFrames AddSplineDoubleKeyFrame(this DoubleAnimationUsingKeyFrames doubleAnimation, TimeSpan time, double value, KeySpline spline = null)
        {
            doubleAnimation.KeyFrames.Add(new SplineDoubleKeyFrame
            {
                KeyTime = KeyTime.FromTimeSpan(time),
                Value = value,
                KeySpline = spline.Clone()
            });
            return doubleAnimation;
        }

        public static ObjectAnimationUsingKeyFrames AddKeyFrame(this ObjectAnimationUsingKeyFrames objectAnimation, double seconds, object value)
        {
            objectAnimation.AddKeyFrame(TimeSpan.FromSeconds(seconds), value);
            return objectAnimation;
        }

        public static ObjectAnimationUsingKeyFrames AddKeyFrame(this ObjectAnimationUsingKeyFrames objectAnimation, TimeSpan time, object value)
        {
            objectAnimation.KeyFrames.Add(new DiscreteObjectKeyFrame
            {
                KeyTime = KeyTime.FromTimeSpan(time),
                Value = value
            });
            return objectAnimation;
        }

        public static T If<T>(this T t, bool condition, Action<T> action) where T : Timeline
        {
            if (condition)
                action(t);

            return t;
        }

        public static ColorAnimationUsingKeyFrames AddKeyFrame(this ColorAnimationUsingKeyFrames colorAnimation, double seconds, Color color)
        {
            colorAnimation.AddKeyFrame(TimeSpan.FromSeconds(seconds), color);
            return colorAnimation;
        }

        public static ColorAnimationUsingKeyFrames AddKeyFrame(this ColorAnimationUsingKeyFrames objectAnimation, TimeSpan time, Color color)
        {
            objectAnimation.KeyFrames.Add(new EasingColorKeyFrame
            {
                KeyTime = KeyTime.FromTimeSpan(time),
                Value = color
            });
            return objectAnimation;
        }

        #endregion

        #region Fluency

        public static DoubleAnimation SetEase(this DoubleAnimation animation, EasingFunctionBase ease)
        {
            animation.EasingFunction = ease;
            return animation;
        }

        public static DoubleAnimation To(this DoubleAnimation animation, double? value)
        {
            animation.To = value;
            return animation;
        }

        public static DoubleAnimation By(this DoubleAnimation animation, double? value)
        {
            animation.By = value;
            return animation;
        }

        public static DoubleAnimation From(this DoubleAnimation animation, double? value)
        {
            animation.From = value;
            return animation;
        }
        public static DoubleAnimation Easing(this DoubleAnimation animation, EasingFunctionBase ease)
        {
            animation.EasingFunction = ease;
            return animation;
        }

        public static T SetSpeedRatio<T>(this T storyboard, double speedRatio) where T : Timeline
        {
            storyboard.SpeedRatio = speedRatio;
            return storyboard;
        }

        /// <summary>
        /// Sets the BeginTime property on a Storyboard
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="storyboard"></param>
        /// <param name="beginTime">Begin time in Seconds</param>
        /// <returns></returns>
        public static T SetBeginTime<T>(this T storyboard, double beginTime) where T : Timeline
        {
            return SetBeginTime(storyboard, TimeSpan.FromSeconds(beginTime));
        }

        public static T SetBeginTime<T>(this T storyboard, TimeSpan beginTime) where T : Timeline
        {
            storyboard.BeginTime = beginTime;
            return storyboard;
        }

        /// <summary>
        /// Sets the Duration of a Timeline
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="storyboard"></param>
        /// <param name="duration">Duration in seconds</param>
        /// <returns></returns>
        public static T SetDuration<T>(this T storyboard, double duration) where T : Timeline
        {
            return SetDuration(storyboard, TimeSpan.FromSeconds(duration));
        }

        public static T SetDuration<T>(this T storyboard, TimeSpan duration) where T : Timeline
        {
            storyboard.Duration = duration;
            return storyboard;
        }

        public static T SetRepeatBehavior<T>(this T storyboard, RepeatBehavior behavior) where T : Timeline
        {
            storyboard.RepeatBehavior = behavior;
            return storyboard;
        }

        public static DoubleAnimation EnableDependentAnimation(this DoubleAnimation storyboard, bool value)
        {
            storyboard.EnableDependentAnimation = value;
            return storyboard;
        }

        public static DoubleAnimationUsingKeyFrames EnableDependentAnimations(this DoubleAnimationUsingKeyFrames storyboard, bool value)
        {
            storyboard.EnableDependentAnimation = value;
            return storyboard;
        }

        public static T CreateTimeline<T>(DependencyObject target, String targetProperty, Storyboard parent) where T : Timeline, new()
        {
            T timeline = new T();
            parent.AddTimeline(timeline, target, targetProperty);
            return timeline;
        }

        public static T CreateTimeline<T>(this Storyboard parent, DependencyObject target, String targetProperty) where T : Timeline, new()
        {
            T timeline = new T();
            parent.AddTimeline(timeline, target, targetProperty);
            return timeline;
        }

        public static Storyboard Build(this Storyboard storyboard, Action<Storyboard> action)
        {
            action(storyboard);
            return storyboard;
        }

        #endregion

        #region Transform3D

        public static CompositeTransform3D GetCompositeTransform3D(this FrameworkElement target)
        {
            if (target.Transform3D is not CompositeTransform3D transform)
            {
                transform = new CompositeTransform3D();
                target.Transform3D = transform;
            }

            return transform;
        }

        public static PerspectiveTransform3D GetPerspectiveTransform3D(this FrameworkElement target)
        {
            if (!(target.Transform3D is PerspectiveTransform3D transform))
            {
                target.Transform3D = transform = new PerspectiveTransform3D();
            }

            return transform;
        }

        #endregion

        #region KeySplines

        private static Vector2 Vec(Point p) => new Vector2((float)p.X, (float)p.Y);
        private static Point Point(Vector2 p) => new Point(p.X, p.Y);

        //public static CubicBezierControlPoints ToBezierPoints(this KeySpline spline)
        //{
        //    return new CubicBezierControlPoints(Vec(spline.ControlPoint1), Vec(spline.ControlPoint2));
        //}

        public static KeySpline CreateKeySpline(Double x1, Double y1, Double x2, Double y2)
        {
            KeySpline keyspline = new KeySpline();
            keyspline.SetPoints(x1, y1, x2, y2);
            return keyspline;
        }

        //public static KeySpline CreateKeySpline(CubicBezierControlPoints controlPoints)
        //{
        //    return new KeySpline()
        //    {
        //        ControlPoint1 = Point(controlPoints.ControlPoint1),
        //        ControlPoint2 = Point(controlPoints.ControlPoint2)
        //    };
        //}

        //public static KeySpline ToKeySpline(this CubicBezierControlPoints controlPoints)
        //    => CreateKeySpline(controlPoints);

        public static KeySpline Reverse(this KeySpline keySpline)
        {
            return new KeySpline
            {
                ControlPoint1 = new Point(keySpline.ControlPoint1.Y, keySpline.ControlPoint1.X),
                ControlPoint2 = new Point(keySpline.ControlPoint2.Y, keySpline.ControlPoint2.X)
            };
        }

        public static KeySpline Clone(this KeySpline keySpline)
        {
            return new KeySpline
            {
                ControlPoint1 = keySpline.ControlPoint1,
                ControlPoint2 = keySpline.ControlPoint2
            };
        }

        public static void SetPoints(this KeySpline keySpline, Double x1, Double y1, Double x2, Double y2)
        {
            keySpline.ControlPoint1 = new Point(x1, y1);
            keySpline.ControlPoint2 = new Point(x2, y2);
        }

        #endregion


    }

    /// <summary>
    /// A collection of common PropertyPath's used by XAML Storyboards
    /// for creating independently animate-able animations. Hence the word
    /// "independent". Don't add CPU bound properties here please. I don't
    /// care for them.
    /// </summary>
    public static class TargetProperty
    {
        //------------------------------------------------------
        //
        // UI Element
        //
        //------------------------------------------------------

        // UIElement Opacity
        public static String Opacity = "(UIElement.Opacity)";
        // UIElement Visibility
        public static String Visibility = "(UIElement.Visibility)";
        // UIElement IsHitTestVisible
        public static String IsHitTestVisible = "(UIElement.IsHitTestVisible)";
        // Grid ColumnSpan
        public static string GridColumnSpan = "(Grid.ColumnSpan)";
        // Grid RowSpan
        public static string GridRowSpan = "(Grid.RowSpan)";



        //------------------------------------------------------
        //
        // Composite Transform (Render Transform)
        //
        //------------------------------------------------------

        public class CompositeTransform
        {
            public static string Identifier { get; } = "(UIElement.RenderTransform).(CompositeTransform.";

            // Render Transform Composite Transform X-Axis Translation
            public static String TranslateX { get; } = "(UIElement.RenderTransform).(CompositeTransform.TranslateX)";
            // Render Transform Composite Transform Y-Axis Translation
            public static String TranslateY { get; } = "(UIElement.RenderTransform).(CompositeTransform.TranslateY)";
            // Render Transform Composite Transform X-Axis Scale
            public static String ScaleX { get; } = "(UIElement.RenderTransform).(CompositeTransform.ScaleX)";
            // Render Transform Composite Transform Y-Axis Scale
            public static String ScaleY { get; } = "(UIElement.RenderTransform).(CompositeTransform.ScaleY)";
            // Render Transform Composite Transform X-Scale Skew
            public static String SkewX { get; } = "(UIElement.RenderTransform).(CompositeTransform.SkewX)";
            // Render Transform Composite Transform Y-Scale Skew
            public static String SkewY { get; } = "(UIElement.RenderTransform).(CompositeTransform.SkewY)";
            // Render Transform Composite Transform Rotation
            public static String Rotation { get; } = "(UIElement.RenderTransform).(CompositeTransform.Rotation)";
        }







        //------------------------------------------------------
        //
        //  Plane Projection
        //
        //------------------------------------------------------

        public class PlaneProjection
        {
            public static string Identifier { get; } = "(UIElement.Projection).(PlaneProjection.";

            // Plane Projection X-Axis Rotation
            public static String RotationX { get; } = "(UIElement.Projection).(PlaneProjection.RotationX)";
            // Plane Projection Y-Axis Rotation
            public static String RotationY { get; } = "(UIElement.Projection).(PlaneProjection.RotationY)";
            // Plane Projection Z-Axis Rotation
            public static String RotationZ { get; } = "(UIElement.Projection).(PlaneProjection.RotationZ)";

            public static String GlobalOffsetX { get; } = "(UIElement.Projection).(PlaneProjection.GlobalOffsetX)";
            public static String GlobalOffsetY { get; } = "(UIElement.Projection).(PlaneProjection.GlobalOffsetY)";
            public static String GlobalOffsetZ { get; } = "(UIElement.Projection).(PlaneProjection.GlobalOffsetZ)";

            public static String LocalOffsetX { get; } = "(UIElement.Projection).(PlaneProjection.LocalOffsetX)";
            public static String LocalOffsetY { get; } = "(UIElement.Projection).(PlaneProjection.LocalOffsetY)";
            public static String LocalOffsetZ { get; } = "(UIElement.Projection).(PlaneProjection.LocalOffsetZ)";

            public static String CenterOfRotationX { get; } = "(UIElement.Projection).(PlaneProjection.CenterOfRotationX)";
            public static String CenterOfRotationY { get; } = "(UIElement.Projection).(PlaneProjection.CenterOfRotationY)";
            public static String CenterOfRotationZ { get; } = "(UIElement.Projection).(PlaneProjection.CenterOfRotationZ)";
        }





        //------------------------------------------------------
        //
        //  Composite Transform 3D (Transform 3D)
        //
        //------------------------------------------------------

        public class CompositeTransform3D
        {
            public static string Identifier { get; } = "(UIElement.Transform3D).(CompositeTransform3D.";

            public static String TranslateX { get; } = "(UIElement.Transform3D).(CompositeTransform3D.TranslateX)";
            public static String TranslateY { get; } = "(UIElement.Transform3D).(CompositeTransform3D.TranslateY)";
            public static String TranslateZ { get; } = "(UIElement.Transform3D).(CompositeTransform3D.TranslateZ)";

            public static String RotationX { get; } = "(UIElement.Transform3D).(CompositeTransform3D.RotationX)";
            public static String RotationY { get; } = "(UIElement.Transform3D).(CompositeTransform3D.RotationY)";
            public static String RotationZ { get; } = "(UIElement.Transform3D).(CompositeTransform3D.RotationZ)";

            public static String ScaleX { get; } = "(UIElement.Transform3D).(CompositeTransform3D.ScaleX)";
            public static String ScaleY { get; } = "(UIElement.Transform3D).(CompositeTransform3D.ScaleY)";
            public static String ScaleZ { get; } = "(UIElement.Transform3D).(CompositeTransform3D.ScaleZ)";

            public static String CenterX { get; } = "(UIElement.Transform3D).(CompositeTransform3D.CenterX)";
            public static String CenterY { get; } = "(UIElement.Transform3D).(CompositeTransform3D.CenterY)";
            public static String CenterZ { get; } = "(UIElement.Transform3D).(CompositeTransform3D.CenterZ)";
        }
    }

    public static class KeySplines
    {
        /// <summary>
        /// Returns a KeySpline for use as an easing function
        /// to replicate the easing of the EntranceThemeTransition
        /// </summary>
        /// <returns></returns>
        public static KeySpline EntranceTheme =>
            Animation.CreateKeySpline(0.1, 0.9, 0.2, 1);

        /// <summary>
        /// A KeySpline that closely matches the default easing curve applied to 
        /// Composition animations by Windows when the developer does not specify
        /// any easing function.
        /// </summary>
        public static KeySpline CompositionDefault =>
            Animation.CreateKeySpline(0.395, 0.56, 0.06, 0.95);

        /// <summary>
        /// Intended for 500 millisecond opacity animation for depth animations
        /// </summary>
        public static KeySpline DepthZoomOpacity =>
            Animation.CreateKeySpline(0.2, 0.6, 0.3, 0.9);

        /// <summary>
        /// A more precise alternative to EntranceTheme KeySpline
        /// </summary>
        public static KeySpline Popup =>
            Animation.CreateKeySpline(0.100000001490116, 0.899999976158142, 0.200000002980232, 1);




        //------------------------------------------------------
        //
        //  Fluent KeySplines
        //
        //------------------------------------------------------

        /* 
            These splines are taken from Microsoft's official animation documentation for 
            fluent animation design system.

            For reference recommended durations are:
                Exit Animations         : 150ms
                Entrance Animations     : 300ms
                Translation Animations  : <= 500ms
        */


        /// <summary>
        /// Analogous to Exponential EaseIn, Exponent 4.5
        /// </summary>
        public static KeySpline FluentAccelerate =>
            Animation.CreateKeySpline(0.7, 0, 1, 0.5);

        /// <summary>
        /// Analogous to Exponential EaseOut, Exponent 7
        /// </summary>
        public static KeySpline FluentDecelerate =>
            Animation.CreateKeySpline(0.1, 0.9, 0.2, 1);

        /// <summary>
        /// Analogous to Circle EaseInOut
        /// </summary>
        public static KeySpline FluentStandard =>
            Animation.CreateKeySpline(0.8, 0, 0.2, 1);





        //------------------------------------------------------
        //
        //  Standard Penner Splines
        //
        //------------------------------------------------------

        public static KeySpline CubicInOut =>
            Animation.CreateKeySpline(0.645, 0.045, 0.355, 1);

        public static KeySpline QuinticInOut =>
            Animation.CreateKeySpline(0.86, 0, 0.07, 1);
    }
}
