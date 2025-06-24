using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

namespace CharacterMap.Helpers;

/// <summary>
/// Helpful extensions methods to enable you to write fluent Composition animations
/// </summary>
public static class Composition
{
    /*
     * NOTE
     * 
     * Type constraints on extension methods do not form part of the method
     * signature used for choosing a correct method. Therefore two extensions
     * with the same parameters but different type constraints will conflict
     * with each other.
     * 
     * Due to this, some methods here use type constraints whereas other that
     * conflict with the XAML storyboarding extensions use explicit type
     * extensions. When adding methods, please keep in mind whether it's 
     * possible some other toolkit might have a similar signature for extensions
     * to form your plan of attack
     */

    #region Fundamentals

    public static T SafeDispose<T>(T disposable) where T : IDisposable
    {
        disposable?.Dispose();
        return default;
    }

    public static Compositor Compositor { get; set; } = Window.Current.Compositor;

    public static void CreateScopedBatch(this Compositor compositor,
        CompositionBatchTypes batchType,
        Action<CompositionScopedBatch> action,
        Action<CompositionScopedBatch> onCompleteAction = null)
    {
        if (action == null)
            throw
              new ArgumentException("Cannot create a scoped batch on an action with null value!",
              nameof(action));

        // Create ScopedBatch
        var batch = compositor.CreateScopedBatch(batchType);

        //// Handler for the Completed Event
        void handler(object s, CompositionBatchCompletedEventArgs ea)
        {
            // Unsubscribe the handler from the Completed Event
            ((CompositionScopedBatch)s).Completed -= handler;

            try
            {
                // Invoke the post action
                onCompleteAction?.Invoke(batch);
            }
            finally
            {
                ((CompositionScopedBatch)s).Dispose();
            }
        }

        batch.Completed += handler;

        // Invoke the action
        action(batch);


        // End Batch
        batch.End();
    }

    private static Dictionary<Compositor, Dictionary<string, object>> _objCache { get; } = new();

    public static T GetCached<T>(this Compositor c, string key, Func<T> create)
    {
//#if DEBUG
//        return create();
//#endif

        if (_objCache.TryGetValue(c, out Dictionary<string, object> dic) is false)
            _objCache[c] = dic = new();

        if (dic.TryGetValue(key, out object value) is false)
            dic[key] = value = create();

        return (T)value;
    }

    /// <summary>
    /// Gets a cached version of a CompositionObject per compositor
    /// (Each CoreWindow has it's own compositor). Allows sharing of animations
    /// without recreating everytime.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="c"></param>
    /// <param name="key"></param>
    /// <param name="create"></param>
    /// <returns></returns>
    public static T GetCached<T>(this CompositionObject c, string key, Func<T> create) where T : CompositionObject
    {
        return GetCached<T>(c.Compositor, key, create);
    }

    public static CubicBezierEasingFunction GetCachedEntranceEase(this Compositor c)
    {
        return c.GetCached<CubicBezierEasingFunction>("EntranceEase",
            () => c.CreateEntranceEasingFunction());
    }

    public static CubicBezierEasingFunction GetCachedFluentEntranceEase(this Compositor c)
    {
        return c.GetCached<CubicBezierEasingFunction>("FluentEntranceEase",
            () => c.CreateFluentEntranceEasingFunction());
    }

    #endregion


    #region Element / Base Extensions

    public static IEnumerable<Visual> GetVisuals<T>(this IEnumerable<T> elements) where T : UIElement
    {
        foreach (var item in elements)
            yield return GetElementVisual(item);
    }

    /// <summary>
    /// Returns the Composition Hand-off Visual for this framework element
    /// </summary>
    /// <param name="element"></param>
    /// <returns>Composition Hand-off Visual</returns>
    public static Visual GetElementVisual(this UIElement element) => element == null ? null : ElementCompositionPreview.GetElementVisual(element);

    public static ContainerVisual GetContainerVisual(this UIElement element, bool linkSize = true)
    {
        if (element == null)
            return null;

        if (ElementCompositionPreview.GetElementChildVisual(element) is ContainerVisual container)
            return container;

        // Create a new container visual, link it's size to the element's and then set
        // the container as the child visual of the element.
        container = GetElementVisual(element).Compositor.CreateContainerVisual();
        CompositionExtensions.LinkSize(container, GetElementVisual(element));
        element.SetChildVisual(container);

        return container;
    }

    public static CompositionPropertySet GetScrollManipulationPropertySet(this ScrollViewer scrollViewer)
    {
        return ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scrollViewer);
    }

    public static void SetShowAnimation(this UIElement element, ICompositionAnimationBase animation)
    {
        ElementCompositionPreview.SetImplicitShowAnimation(element, animation);
    }

    public static void SetHideAnimation(this UIElement element, ICompositionAnimationBase animation)
    {
        ElementCompositionPreview.SetImplicitHideAnimation(element, animation);
    }

    public static T SetChildVisual<T>(this T element, Visual visual) where T : UIElement
    {
        ElementCompositionPreview.SetElementChildVisual(element, visual);
        return element;
    }

    public static bool SupportsAlphaMask(UIElement element)
    {
        return element switch
        {
            TextBlock _ or Shape _ or Image _ => true,
            _ => false,
        };
    }

    public static InsetClip ClipToBounds(UIElement element)
    {
        var v = GetElementVisual(element);
        var c = v.Compositor.CreateInsetClip();
        v.Clip = c;
        return c;
    }

    /// <summary>
    /// Attempts to get the AlphaMask from supported UI elements.
    /// Returns null if the element cannot create an AlphaMask.
    /// </summary>
    /// <param name="element"></param>
    /// <returns></returns>
    public static CompositionBrush GetAlphaMask(UIElement element)
    {
        switch (element)
        {
            case TextBlock t:
                return t.GetAlphaMask();

            case Shape s:
                return s.GetAlphaMask();

            case Image i:
                return i.GetAlphaMask();

            default:
                break;
        }

        //if (element is ISupportsAlphaMask mask)
        //    return mask.GetAlphaMask();

        return null;
    }

    #endregion


    #region Translation

    //public static IEnumerable<T> EnableCompositionTranslation<T>(this IEnumerable<T> elements) where T : UIElement
    //{
    //    foreach (var element in elements)
    //    {
    //        element.EnableCompositionTranslation();
    //        yield return element;
    //    }
    //}

    public static UIElement EnableCompositionTranslation(this UIElement element)
    {
        return EnableCompositionTranslation(element, null);
    }

    public static UIElement EnableCompositionTranslation(this UIElement element, float x, float y, float z)
    {
        return EnableCompositionTranslation(element, new Vector3(x, y, z));
    }

    public static UIElement EnableCompositionTranslation(this UIElement element, Vector3? defaultTranslation)
    {
        Visual visual = GetElementVisual(element);
        if (visual.Properties.TryGetVector3(CompositionFactory.TRANSLATION, out _) == CompositionGetValueStatus.NotFound)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(element, true);
            if (defaultTranslation.HasValue)
                visual.Properties.InsertVector3(CompositionFactory.TRANSLATION, defaultTranslation.Value);
            else
                visual.Properties.InsertVector3(CompositionFactory.TRANSLATION, new Vector3());
        }

        return element;
    }

    public static bool IsTranslationEnabled(this UIElement element)
    {
        return GetElementVisual(element).Properties.TryGetVector3(CompositionFactory.TRANSLATION, out _) != CompositionGetValueStatus.NotFound;
    }

    public static Vector3 GetTranslation(this Visual visual)
    {
        visual.Properties.TryGetVector3(CompositionFactory.TRANSLATION, out Vector3 translation);
        return translation;
    }

    public static Visual SetTranslation(this Visual visual, float x, float y, float z)
    {
        return SetTranslation(visual, new Vector3(x, y, z));
    }

    public static Visual SetTranslation(this Visual visual, Vector3 translation)
    {
        visual.Properties.InsertVector3(CompositionFactory.TRANSLATION, translation);
        return visual;
    }

    /// <summary>
    /// Sets the axis to rotate the visual around.
    /// </summary>
    /// <param name="visual"></param>
    /// <param name="axis"></param>
    /// <returns></returns>
    public static Visual SetRotationAxis(this Visual visual, Vector3 axis)
    {
        visual.RotationAxis = axis;
        return visual;
    }

    /// <summary>
    /// Sets the axis to rotate the visual around.
    /// </summary>
    /// <param name="visual"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public static Visual SetRotationAxis(this Visual visual, float x, float y, float z)
    {
        visual.RotationAxis = new Vector3(x, y, z);
        return visual;
    }

    public static Visual SetCenterPoint(this Visual visual, float x, float y, float z)
    {
        return SetCenterPoint(visual, new Vector3(x, y, z));
    }

    public static Visual SetCenterPoint(this Visual visual, Vector3 vector)
    {
        visual.CenterPoint = vector;
        return visual;
    }

    /// <summary>
    /// Sets the centre point of a visual to its current cartesian centre (relative 0.5f, 0.5f).
    /// </summary>
    /// <param name="visual"></param>
    /// <returns></returns>
    public static Visual SetCenterPoint(this Visual visual)
    {
        return SetCenterPoint(visual, new Vector3(visual.Size / 2f, 0f));
    }

    #endregion


    #region SetTarget

    public static T SetTarget<T>(this T animation, string target) where T : CompositionAnimation
    {
        animation.Target = target;
        return animation;
    }

    public static T SetSafeTarget<T>(this T animation, string target) where T : CompositionAnimation
    {
        if (!String.IsNullOrEmpty(target))
            animation.Target = target;

        return animation;
    }

    #endregion

    #region SetDelayTime

    /// <summary>
    /// Sets the delay time in seconds
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="animation"></param>
    /// <param name="delayTime">Delay Time in seconds</param>
    /// <returns></returns>
    public static T SetDelayTime<T>(this T animation, double delayTime)
        where T : KeyFrameAnimation
    {
        animation.DelayTime = TimeSpan.FromSeconds(delayTime);
        return animation;
    }

    public static T SetDelayTime<T>(this T animation, TimeSpan delayTime)
        where T : KeyFrameAnimation
    {
        animation.DelayTime = delayTime;
        return animation;
    }

    #endregion


    #region SetDelay

    /// <summary>
    /// Sets the delay time in seconds
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="animation"></param>
    /// <param name="delayTime">Delay Time in seconds</param>
    /// <returns></returns>
    public static T SetDelay<T>(this T animation, double delayTime, AnimationDelayBehavior behavior)
        where T : KeyFrameAnimation
    {
        animation.DelayTime = TimeSpan.FromSeconds(delayTime);
        animation.DelayBehavior = behavior;
        return animation;
    }

    public static T SetDelay<T>(this T animation, TimeSpan delayTime, AnimationDelayBehavior behavior)
        where T : KeyFrameAnimation
    {
        animation.DelayBehavior = behavior;
        animation.DelayTime = delayTime;
        return animation;
    }

    #endregion


    #region SetDelayBehaviour

    public static T SetDelayBehavior<T>(this T animation, AnimationDelayBehavior behavior)
        where T : KeyFrameAnimation
    {
        animation.DelayBehavior = behavior;
        return animation;
    }

    public static T SetInitialValueBeforeDelay<T>(this T animation)
       where T : KeyFrameAnimation
    {
        animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        return animation;
    }

    #endregion


    #region SetDuration

    /// <summary>
    /// Sets the duration in seconds. If less than 0 the duration is not set.
    /// </summary>
    /// <param name="animation"></param>
    /// <param name="duration">Duration in seconds</param>
    /// <returns></returns>
    public static T SetDuration<T>(this T animation, double duration) where T : KeyFrameAnimation
    {
        if (duration >= 0)
            return SetDuration(animation, TimeSpan.FromSeconds(duration));
        else
            return animation;
    }

    public static T SetDuration<T>(this T animation, TimeSpan duration) where T : KeyFrameAnimation
    {
        animation.Duration = duration;
        return animation;
    }

    #endregion


    #region StopBehaviour

    public static T SetStopBehavior<T>(this T animation, AnimationStopBehavior stopBehavior) where T : KeyFrameAnimation
    {
        animation.StopBehavior = stopBehavior;
        return animation;
    }

    #endregion


    #region Direction

    public static T SetDirection<T>(this T animation, AnimationDirection direction) where T : KeyFrameAnimation
    {
        animation.Direction = direction;
        return animation;
    }

    #endregion


    #region Comment

    public static T SetComment<T>(this T obj, string comment) where T : CompositionObject
    {
        obj.Comment = comment;
        return obj;
    }

    #endregion


    #region IterationBehavior

    public static T SetIterationBehavior<T>(this T animation, AnimationIterationBehavior iterationBehavior) where T : KeyFrameAnimation
    {
        animation.IterationBehavior = iterationBehavior;
        return animation;
    }

    #endregion


    #region AddKeyFrame Builders

    public static T SetFinalValue<T>(this T animation, Vector3 finalValue) where T : Vector3NaturalMotionAnimation
    {
        animation.FinalValue = finalValue;
        return animation;
    }

    public static T SetFinalValue<T>(this T animation, float x, float y, float z) where T : Vector3NaturalMotionAnimation
    {
        animation.FinalValue = new Vector3(x, y, z);
        return animation;
    }

    public static T AddKeyFrame<T>(this T animation, float normalizedProgressKey, string expression, KeySpline spline) where T : KeyFrameAnimation
    {
        animation.InsertExpressionKeyFrame(normalizedProgressKey, expression, animation.Compositor.CreateCubicBezierEasingFunction(spline));
        return animation;
    }

    public static T AddKeyFrame<T>(this T animation, float normalizedProgressKey, string expression, CubicBezierPoints spline) where T : KeyFrameAnimation
    {
        animation.InsertExpressionKeyFrame(normalizedProgressKey, expression, animation.Compositor.CreateCubicBezierEasingFunction(spline));
        return animation;
    }

    public static T AddKeyFrame<T>(this T animation, float normalizedProgressKey, string expression, CompositionEasingFunction ease = null) where T : KeyFrameAnimation
    {
        animation.InsertExpressionKeyFrame(normalizedProgressKey, expression, ease);
        return animation;
    }

    public static ScalarKeyFrameAnimation AddKeyFrame(this ScalarKeyFrameAnimation animation, float normalizedProgressKey, float value, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, ease);
        return animation;
    }

    public static ScalarKeyFrameAnimation AddKeyFrame(this ScalarKeyFrameAnimation animation, float normalizedProgressKey, float value, KeySpline ease)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, animation.Compositor.CreateCubicBezierEasingFunction(ease));
        return animation;
    }

    public static ColorKeyFrameAnimation AddKeyFrame(this ColorKeyFrameAnimation animation, float normalizedProgressKey, Color value, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, ease);
        return animation;
    }

    public static Vector2KeyFrameAnimation AddKeyFrame(this Vector2KeyFrameAnimation animation, float normalizedProgressKey, Vector2 value, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, ease);
        return animation;
    }

    public static Vector2KeyFrameAnimation AddKeyFrame(this Vector2KeyFrameAnimation animation, float normalizedProgressKey, Vector2 value, KeySpline ease)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, animation.Compositor.CreateCubicBezierEasingFunction(ease));
        return animation;
    }

    #region Vector3

    public static Vector3KeyFrameAnimation AddKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, Vector3 value, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, ease);
        return animation;
    }

    public static Vector3KeyFrameAnimation AddKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, Vector3 value, KeySpline ease)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, animation.Compositor.CreateCubicBezierEasingFunction(ease));
        return animation;
    }

    public static Vector3KeyFrameAnimation AddKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, Vector3 value, CubicBezierPoints ease)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, animation.Compositor.CreateCubicBezierEasingFunction(ease));
        return animation;
    }

    /// <summary>
    /// Adds a Vector3KeyFrame where the X & Y components are set to the input value and the Z component defaults to 0f.
    /// </summary>
    /// <param name="animation"></param>
    /// <param name="normalizedProgressKey"></param>
    /// <param name="value"></param>
    /// <param name="ease"></param>
    /// <returns></returns>
    public static Vector3KeyFrameAnimation AddKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, float value, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, new Vector3(value, value, 0f), ease);
        return animation;
    }

    public static Vector3KeyFrameAnimation AddKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, float value, KeySpline ease)
    {
        animation.InsertKeyFrame(normalizedProgressKey, new Vector3(value, value, 0f), animation.Compositor.CreateCubicBezierEasingFunction(ease));
        return animation;
    }

    /// <summary>
    /// Adds a Vector3KeyFrame where the X & Y components are set to the input value and the Z component defaults to 1f.
    /// </summary>
    /// <param name="animation"></param>
    /// <param name="normalizedProgressKey"></param>
    /// <param name="value"></param>
    /// <param name="ease"></param>
    /// <returns></returns>
    public static Vector3KeyFrameAnimation AddScaleKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, float value, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, new Vector3(value, value, 1f), ease);
        return animation;
    }

    /// <summary>
    /// Adds a Vector3KeyFrame where the X & Y components are set to the input value and the Z component defaults to 1f.
    /// </summary>
    /// <param name="animation"></param>
    /// <param name="normalizedProgressKey"></param>
    /// <param name="value"></param>
    /// <param name="ease"></param>
    /// <returns></returns>
    public static Vector3KeyFrameAnimation AddScaleKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, float value, KeySpline ease)
    {
        animation.InsertKeyFrame(normalizedProgressKey, new Vector3(value, value, 1f), animation.Compositor.CreateCubicBezierEasingFunction(ease));
        return animation;
    }

    /// <summary>
    /// Adds a Vector3KeyFrame using the X & Y components. The Z component defaults to 0f.
    /// </summary>
    /// <param name="animation"></param>
    /// <param name="normalizedProgressKey"></param>
    /// <param name="x">X Component of the Vector3</param>
    /// <param name="y">Y Component of the Vector3</param>
    /// <param name="ease">Optional ease</param>
    /// <returns></returns>
    public static Vector3KeyFrameAnimation AddKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, float x, float y, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, new Vector3(x, y, 0f), ease);
        return animation;
    }

    public static Vector3KeyFrameAnimation AddKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, float x, float y, KeySpline ease)
    {
        animation.InsertKeyFrame(normalizedProgressKey, new Vector3(x, y, 0f), animation.Compositor.CreateCubicBezierEasingFunction(ease));
        return animation;
    }

    /// <summary>
    /// Adds a Vector3KeyFrame with X Y & Z components.
    /// </summary>
    /// <param name="animation"></param>
    /// <param name="normalizedProgressKey"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="ease"></param>
    /// <returns></returns>
    public static Vector3KeyFrameAnimation AddKeyFrame(this Vector3KeyFrameAnimation animation, float normalizedProgressKey, float x, float y, float z, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, new Vector3(x, y, z), ease);
        return animation;
    }

    #endregion

    public static Vector4KeyFrameAnimation AddKeyFrame(this Vector4KeyFrameAnimation animation, float normalizedProgressKey, Vector4 value, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, ease);
        return animation;
    }

    public static QuaternionKeyFrameAnimation AddKeyFrame(this QuaternionKeyFrameAnimation animation, float normalizedProgressKey, Quaternion value, CompositionEasingFunction ease = null)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, ease);
        return animation;
    }

    public static QuaternionKeyFrameAnimation AddKeyFrame(this QuaternionKeyFrameAnimation animation, float normalizedProgressKey, Quaternion value, KeySpline ease)
    {
        animation.InsertKeyFrame(normalizedProgressKey, value, animation.Compositor.CreateCubicBezierEasingFunction(ease));
        return animation;
    }

    #endregion


    #region Compositor Create Builders

    private static T TryAddGroup<T>(CompositionObject obj, T animation) where T : CompositionAnimation
    {
        if (obj is CompositionAnimationGroup group)
            group.Add(animation);

        return animation;
    }

    public static SpringVector3NaturalMotionAnimation CreateSpringVector3Animation(
        this CompositionObject visual, string targetProperty = null)
    {
        return TryAddGroup(visual, visual.Compositor.CreateSpringVector3Animation().SetSafeTarget(targetProperty));
    }

    public static ColorKeyFrameAnimation CreateColorKeyFrameAnimation(this CompositionObject visual, string targetProperty = null)
    {
        return TryAddGroup(visual, visual.Compositor.CreateColorKeyFrameAnimation().SetSafeTarget(targetProperty));
    }

    public static ScalarKeyFrameAnimation CreateScalarKeyFrameAnimation(this CompositionObject visual, string targetProperty = null)
    {
        return TryAddGroup(visual, visual.Compositor.CreateScalarKeyFrameAnimation().SetSafeTarget(targetProperty));
    }

    public static Vector2KeyFrameAnimation CreateVector2KeyFrameAnimation(this CompositionObject visual, string targetProperty = null)
    {
        return TryAddGroup(visual, visual.Compositor.CreateVector2KeyFrameAnimation().SetSafeTarget(targetProperty));
    }

    public static Vector3KeyFrameAnimation CreateVector3KeyFrameAnimation(this CompositionObject visual, string targetProperty = null)
    {
        return TryAddGroup(visual, visual.Compositor.CreateVector3KeyFrameAnimation().SetSafeTarget(targetProperty));
    }

    public static Vector4KeyFrameAnimation CreateVector4KeyFrameAnimation(this CompositionObject visual, string targetProperty = null)
    {
        return TryAddGroup(visual, visual.Compositor.CreateVector4KeyFrameAnimation().SetSafeTarget(targetProperty));
    }

    public static QuaternionKeyFrameAnimation CreateQuaternionKeyFrameAnimation(this CompositionObject visual, string targetProperty = null)
    {
        return TryAddGroup(visual, visual.Compositor.CreateQuaternionKeyFrameAnimation().SetSafeTarget(targetProperty));
    }

    public static ExpressionAnimation CreateExpressionAnimation(this CompositionObject visual)
    {
        return TryAddGroup(visual, visual.Compositor.CreateExpressionAnimation());
    }

    public static ExpressionAnimation CreateExpressionAnimation(this CompositionObject visual, string targetProperty)
    {
        return TryAddGroup(visual, visual.Compositor.CreateExpressionAnimation().SetTarget(targetProperty));
    }

    public static CubicBezierEasingFunction CreateCubicBezierEasingFunction(this Compositor compositor, float x1, float y1, float x2, float y2)
    {
        return compositor.CreateCubicBezierEasingFunction(new Vector2(x1, y1), new Vector2(x2, y2));
    }

    public static CubicBezierEasingFunction CreateCubicBezierEasingFunction(this Compositor compositor, Windows.UI.Xaml.Media.Animation.KeySpline spline)
    {
        return compositor.CreateCubicBezierEasingFunction(spline.ControlPoint1.ToVector2(), spline.ControlPoint2.ToVector2());
    }

    public static CubicBezierEasingFunction CreateCubicBezierEasingFunction(this Compositor compositor, CubicBezierPoints points)
    {
        return compositor.CreateCubicBezierEasingFunction(points.Start, points.End);
    }

    #endregion


    #region SetExpression

    public static ExpressionAnimation SetExpression(this ExpressionAnimation animation, string expression)
    {
        if (!string.IsNullOrWhiteSpace(expression))
            animation.Expression = expression;

        return animation;
    }

    #endregion  


    #region SetParameter Builders

    public static T SetParameter<T>(this T animation, string key, UIElement parameter) where T : CompositionAnimation
    {
        if (parameter != null)
            animation.SetReferenceParameter(key, GetElementVisual(parameter));

        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, CompositionObject parameter) where T : CompositionAnimation
    {
        animation.SetReferenceParameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, float parameter) where T : CompositionAnimation
    {
        animation.SetScalarParameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, double parameter) where T : CompositionAnimation
    {
        animation.SetScalarParameter(key, (float)parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, bool parameter) where T : CompositionAnimation
    {
        animation.SetBooleanParameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, Vector2 parameter) where T : CompositionAnimation
    {
        animation.SetVector2Parameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, Vector3 parameter) where T : CompositionAnimation
    {
        animation.SetVector3Parameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, Vector4 parameter) where T : CompositionAnimation
    {
        animation.SetVector4Parameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, Matrix3x2 parameter) where T : CompositionAnimation
    {
        animation.SetMatrix3x2Parameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, Matrix4x4 parameter) where T : CompositionAnimation
    {
        animation.SetMatrix4x4Parameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, Quaternion parameter) where T : CompositionAnimation
    {
        animation.SetQuaternionParameter(key, parameter);
        return animation;
    }

    public static T SetParameter<T>(this T animation, string key, IAnimationObject parameter) where T : CompositionAnimation
    {
        animation.SetExpressionReferenceParameter(key, parameter);
        return animation;
    }

    #endregion


    #region PropertySet Builders

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, float value)
    {
        set.InsertScalar(name, value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, bool value)
    {
        set.InsertBoolean(name, value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, Vector2 value)
    {
        set.InsertVector2(name, value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, Vector3 value)
    {
        set.InsertVector3(name, value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, Vector4 value)
    {
        set.InsertVector4(name, value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, Color value)
    {
        set.InsertColor(name, value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, Matrix3x2 value)
    {
        set.InsertMatrix3x2(name, value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, Matrix4x4 value)
    {
        set.InsertMatrix4x4(name, value);
        return set;
    }

    public static CompositionPropertySet Insert(this CompositionPropertySet set, string name, Quaternion value)
    {
        set.InsertQuaternion(name, value);
        return set;
    }

    #endregion


    #region Animation Start / Stop

    public static void StartAnimation(this CompositionObject compositionObject, CompositionAnimation animation)
    {
        if (string.IsNullOrWhiteSpace(animation.Target))
            throw new ArgumentNullException("Animation has no target");

      compositionObject.StartAnimation(animation.Target, animation);
    }

    public static void StartAnimation(this CompositionObject compositionObject, CompositionAnimationGroup animation)
    {
        compositionObject.StartAnimationGroup(animation);
    }

    public static void StopAnimation(this CompositionObject compositionObject, CompositionAnimation animation)
    {
        if (string.IsNullOrWhiteSpace(animation.Target))
            throw new ArgumentNullException("Animation has no target");

        compositionObject.StopAnimation(animation.Target);
    }

    #endregion


    #region Brushes

    public static CompositionGradientBrush AsCompositionBrush(this LinearGradientBrush brush, Compositor compositor)
    {
        var compBrush = compositor.CreateLinearGradientBrush();

        foreach (var stop in brush.GradientStops)
        {
            compBrush.ColorStops.Add(compositor.CreateColorGradientStop((float)stop.Offset, stop.Color));
        }

        // todo : try and copy transforms?

        return compBrush;
    }

    #endregion


    #region Extras

    public static CubicBezierEasingFunction CreateEase(this Compositor c, float x1, float y1, float x2, float y2)
    {
        return c.CreateCubicBezierEasingFunction(new(x1, y1), new(x2, y2));
    }

    public static CubicBezierEasingFunction CreateEntranceEasingFunction(this Compositor c)
    {
        return c.CreateCubicBezierEasingFunction(new(.1f, .9f), new(.2f, 1));
    }

    public static CubicBezierEasingFunction CreateFluentEntranceEasingFunction(this Compositor c)
    {
        return c.CreateCubicBezierEasingFunction(new(0f, 0f), new(0f, 1));
    }

    public static CompositionAnimationGroup CreateAnimationGroup(this Compositor c, params CompositionAnimation[] animations)
    {
        var group = c.CreateAnimationGroup();
        foreach (var a in animations)
            group.Add(a);
        return group;
    }


    public static bool HasImplicitAnimation<T>(this T c, string path) where T: CompositionObject
    {
        return c.ImplicitAnimations != null 
            && c.ImplicitAnimations.TryGetValue(path, out ICompositionAnimationBase v)
            && v != null;
    }

    public static T SetImplicitAnimation<T>(this T composition, string path, ICompositionAnimationBase animation)
        where T : CompositionObject
    {
        if (composition.ImplicitAnimations == null)
        {
            if (animation == null)
                return composition;

            composition.ImplicitAnimations = composition.Compositor.CreateImplicitAnimationCollection();
        }

        if (animation == null)
            composition.ImplicitAnimations.Remove(path);
        else
            composition.ImplicitAnimations[path] = animation;

        return composition;
    }

    public static FrameworkElement SetImplicitAnimation(this FrameworkElement element, string path, ICompositionAnimationBase animation)
    {
        SetImplicitAnimation(GetElementVisual(element), path, animation);
        return element;
    }

    #endregion





    public static XAMLAnimationCollection GetAnimations(DependencyObject obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        // Ensure there is always a collection when accessed via code
        XAMLAnimationCollection collection = (XAMLAnimationCollection)obj.GetValue(AnimationsProperty);
        if (collection == null)
        {
            collection = new ();
            obj.SetValue(AnimationsProperty, collection);
        }

        return collection;
    }

    public static void SetAnimations(DependencyObject obj, XAMLAnimationCollection value)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));
        obj.SetValue(AnimationsProperty, value);

        if (value is not null)
        {
            value.Attach(obj);
        }
    }

    public static readonly DependencyProperty AnimationsProperty =
        DependencyProperty.RegisterAttached(
            "Animations",
            typeof(XAMLAnimationCollection),
            typeof(Composition),
            new PropertyMetadata(null, (s, e) =>
            {
                if (e.NewValue == e.OldValue || s is not FrameworkElement obj)
                    return;

                if (e.OldValue is XAMLAnimationCollection old)
                {
                    obj.Loaded -= Obj_Loaded;
                    obj.Unloaded -= Obj_Unloaded;
                    old.Detach(obj);
                }

                if (e.NewValue is XAMLAnimationCollection collection)
                {
                    obj.Loaded -= Obj_Loaded;
                    obj.Unloaded -= Obj_Unloaded;

                    obj.Loaded += Obj_Loaded;
                    obj.Unloaded += Obj_Unloaded;

                    collection.Attach(obj);
                }
            }));

    private static void Obj_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement f)
        {
            if (GetAnimations(f) is { } a)
                a.Attach(f);
        }
    }

    private static void Obj_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement f && GetAnimations(f) is { } a)
            a.Detach(f);
    }

}

public interface IXamlCompositionAnimationBase
{
    void Start(UIElement target);

    void Stop(UIElement target);
}

public enum UIElementReferenceType
{
    /// <summary>
    /// Gets the composition handoff visual for the UIElement and uses it as the reference.
    /// </summary>
    ElementVisual,
    /// <summary>
    /// Adds the UIElements as an IAnimationObject reference.
    /// </summary>
    AnimationObject,
    /// <summary>
    /// Adds the UIElements as reference to it's own handoff visual's PropertySet
    /// </summary>
    PropertySet
}

public enum ParameterBindingMode
{
    /// <summary>
    /// Default composition behaviour - the parameter is set at the start of the animation.
    /// </summary>
    AtStart,
    /// <summary>
    /// Updates the value by re-applying the animation on change
    /// </summary>
    Live
}

public interface IHandleableEvent
{
    bool Handled { get; set; }
}
public class ParameterUpdatedEventArgs : EventArgs, IHandleableEvent
{
    public ParameterUpdatedEventArgs(AnimationParameter parameter)
    {
        Parameter = parameter;
    }
    public AnimationParameter Parameter { get; }

    public bool Handled { get; set; }
}

[DependencyProperty<string>("Key")]
[DependencyProperty<object>("Value", default, nameof(Update))]
[DependencyProperty<UIElementReferenceType>("UIElementReferenceType", UIElementReferenceType.ElementVisual, nameof(Update))]
[DependencyProperty<ParameterBindingMode>("BindingMode", ParameterBindingMode.AtStart)]
public partial class AnimationParameter : DependencyObject, IEquatable<AnimationParameter>
{
    private WeakReference<CompositionAnimation> _animation;

    /// <summary>
    /// Fires only when BindingMode is set to Live.
    /// </summary>
    public event EventHandler<ParameterUpdatedEventArgs> Updated;

    public void AttachTo(CompositionAnimation ani)
    {
        _animation = new (ani);
        Update();
    }

    public void Detach()
    {
        // Called when removed from - clear out the value we set
        if (_animation?.TryGetTarget(out CompositionAnimation animation) is true)
        {
            if (!string.IsNullOrWhiteSpace(Key))
                animation.ClearParameter(Key);
        }
        
        _animation = null;
    }


    partial void OnKeyChanged(string o, string n)
    {
        if (_animation?.TryGetTarget(out CompositionAnimation animation) is true)
        {
            if (!string.IsNullOrWhiteSpace(o))
                animation.ClearParameter(o);

            if (!string.IsNullOrWhiteSpace(n))
                Update();
        }
    }

    void Update()
    {
        if (_animation is null
            || ReadLocalValue(KeyProperty) == DependencyProperty.UnsetValue
            || ReadLocalValue(ValueProperty) == DependencyProperty.UnsetValue)
            return;

        if (_animation?.TryGetTarget(out CompositionAnimation animation) is true)
        {
            if (Value is float f)
                animation.SetScalarParameter(Key, f);
            else if (Value is double d)
                animation.SetScalarParameter(Key, (float)d);
            else if (Value is int i)
                animation.SetScalarParameter(Key, (float)i);
            else if (Value is Point p)
                animation.SetVector2Parameter(Key, p.ToVector2());
            else if (Value is Vector2 v2)
                animation.SetVector2Parameter(Key, v2);
            else if (Value is Vector3 v3)
                animation.SetVector3Parameter(Key, v3);
            else if (Value is Vector4 v4)
                animation.SetVector4Parameter(Key, v4);
            else if (Value is Matrix3x2 m3)
                animation.SetMatrix3x2Parameter(Key, m3);
            else if (Value is Matrix4x4 m4)
                animation.SetMatrix4x4Parameter(Key, m4);
            else if (Value is Quaternion q)
                animation.SetQuaternionParameter(Key, q);
            else if (Value is bool b)
                animation.SetBooleanParameter(Key, b);
            else if (Value is Color c)
                animation.SetColorParameter(Key, c);
            else if (Value is CompositionObject compObj)
                animation.SetReferenceParameter(Key, compObj);
            else if (Value is IAnimationObject ani && UIElementReferenceType == UIElementReferenceType.AnimationObject)
                animation.SetExpressionReferenceParameter(Key, ani);
            else if (Value is UIElement element && UIElementReferenceType == UIElementReferenceType.ElementVisual)
                animation.SetReferenceParameter(Key, ElementCompositionPreview.GetElementVisual(element));
            else if (Value is UIElement uep && UIElementReferenceType == UIElementReferenceType.PropertySet)
                animation.SetReferenceParameter(Key, ElementCompositionPreview.GetElementVisual(uep).Properties);
            else if (Value is string s && double.TryParse(s, out double ds))
                animation.SetScalarParameter(Key, (float)ds);
            //else
                
            //    throw new NotSupportedException($"Unsupported type for AnimationReferenceParameter: {Value.GetType()}");
        
            // If the binding mode is live, we need to update the parameter when it changes
            if (BindingMode == ParameterBindingMode.Live)
            {
                Updated?.Invoke(this, new(this));
            }
        }
    }



    #region Comparison
    public override bool Equals(object obj)
    {
        return Equals(obj as AnimationParameter);
    }

    public bool Equals(AnimationParameter other)
    {
        return other is not null &&
               Key == other.Key &&
               EqualityComparer<object>.Default.Equals(Value, other.Value) &&
               UIElementReferenceType == other.UIElementReferenceType;
    }

    public override int GetHashCode()
    {
        int hashCode = -70591176;
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Key);
        hashCode = hashCode * -1521134295 + EqualityComparer<object>.Default.GetHashCode(Value);
        hashCode = hashCode * -1521134295 + UIElementReferenceType.GetHashCode();
        return hashCode;
    }

    public static bool operator ==(AnimationParameter left, AnimationParameter right)
    {
        return EqualityComparer<AnimationParameter>.Default.Equals(left, right);
    }

    public static bool operator !=(AnimationParameter left, AnimationParameter right)
    {
        return !(left == right);
    }

    #endregion
}

public class AnimationUpdatedEventArgs : EventArgs, IHandleableEvent
{
    public AnimationUpdatedEventArgs(XamlCompositionAnimationBase animation)
    {
        Animation = animation;
    }
    public XamlCompositionAnimationBase Animation { get; }

    public bool Handled { get; set; }
}

[ContentProperty(Name = nameof(Parameters))]
[DependencyProperty<string>("Target", default, nameof(UpdateTarget))]
public partial class XamlCompositionAnimationBase : DependencyObject, IXamlCompositionAnimationBase
{
    public AnimationParameterCollection Parameters
    {
        get {
            if (GetValue(ParametersProperty) is null)
                SetValue(ParametersProperty, new AnimationParameterCollection());
            return (AnimationParameterCollection)GetValue(ParametersProperty); }
        private set { SetValue(ParametersProperty, value); }
    }

    public static readonly DependencyProperty ParametersProperty =
        DependencyProperty.Register(nameof(Parameters), typeof(AnimationParameterCollection), typeof(XamlCompositionAnimationBase), new PropertyMetadata(null, (d,e) =>
        {
            if (d is XamlCompositionAnimationBase x)
            {
                if (e.OldValue is AnimationParameterCollection old)
                    x.ClearCollection(old);

                if (e.NewValue is AnimationParameterCollection c)
                    x.SetCollection(c);
            }
        }));


    public event EventHandler<ParameterUpdatedEventArgs> ParameterBindingUpdated;

    public event EventHandler<AnimationUpdatedEventArgs> AnimationUpdated;

    protected Compositor Compositor => Window.Current.Compositor;

    protected CompositionAnimation Animation;

    protected void FireAnimationUpdated()
    {
        AnimationUpdated?.Invoke(this, new (this));
    }

    protected void SetAnimation(CompositionAnimation animation)
    {
        Animation = animation;

        // Detaches parameters from any previous animation and attaches them to the new one
        Parameters.SetTarget(animation);

        // Set properties
        UpdateTarget();

        FireAnimationUpdated();
    }



    private void SetCollection(AnimationParameterCollection c)
    {
        c.BindingUpdated -= ParameterBinding_Updated;
        c.BindingUpdated += ParameterBinding_Updated;
        c.SetTarget(this.Animation);
    }

    private void ClearCollection(AnimationParameterCollection old)
    {
        old.BindingUpdated -= ParameterBinding_Updated;
        old.SetTarget(null);
    }

    private void ParameterBinding_Updated(object sender, ParameterUpdatedEventArgs e)
    {
        if (e.Handled is false)
            this.ParameterBindingUpdated?.Invoke(this, e);
    }




    /* Animation Properties */
    void UpdateTarget() => Animation?.SetSafeTarget(Target);
    



    /* Animation Control */

    public virtual void Start(UIElement target)
    {
        if (target is not null)
        {
            if (this.Target == CompositionFactory.TRANSLATION)
                target.EnableCompositionTranslation();


            // Cannot play if parameters are not set
            if (Parameters.OfType<AnimationParameter>().Any(p => p.Value is null))
                return;

            // Cannot play blank target
            if (string.IsNullOrWhiteSpace(Animation.Target))
                return;

            target?.GetElementVisual()?.StartAnimation(Animation);
        }
    }

    public virtual void Stop(UIElement target)
    {
        target?.GetElementVisual()?.StopAnimation(Animation);
    }
}

[DependencyProperty<string>("Expression")]
public partial class XAMLExpressionAnimation : XamlCompositionAnimationBase
{
    public XAMLExpressionAnimation()
    {
        SetAnimation(
            Compositor.CreateExpressionAnimation().SetExpression(Fix(Expression)));
    }

    string Fix(string s)
    {
        return s?.Replace("'", "\"");
    }

    partial void OnExpressionChanged(string o, string n)
    {
        if (Animation is ExpressionAnimation e)
        {
            e.SetExpression(Fix(n));
            base.FireAnimationUpdated();
        }
    }
}

public sealed partial class XAMLAnimationCollection : DependencyObjectCollection
{
    // After a VectorChanged event we need to compare the current state of the collection
    // with the old collection so that we can call Detach on all removed items.
    private List<IXamlCompositionAnimationBase> _oldCollection { get; } = new ();

    private List<WeakReference<DependencyObject>> _associated { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="XAMLAnimationCollection"/> class.
    /// </summary>
    public XAMLAnimationCollection()
    {
        this.VectorChanged += this.OnVectorChanged;
    }

    void Trim()
    {
        // Remove any dead WeakReferences from the associated list
        for (int i = _associated.Count - 1; i >= 0; i--)
            if (!_associated[i].TryGetTarget(out DependencyObject target) || target is null)
                _associated.RemoveAt(i);
    }

    /// <summary>
    /// Attaches the collection of animations to the specified <see cref="DependencyObject"/>.
    /// </summary>
    /// <param name="associatedObject">The <see cref="DependencyObject"/> to which to attach.</param>
    /// </exception>
    public void Attach(DependencyObject associatedObject)
    {
        Trim();

        // Check if we're loaded first, otherwise x:Bind will not have run yet
        if (VisualTreeHelper.GetParent(associatedObject) is null)
            return;

        // Do not attach if the object is already associated
        if (_associated.Any(x => x.TryGetTarget(out DependencyObject target) && target == associatedObject))
            return;

        // Store a WeakReference to the associated object
        _associated.Add(new WeakReference<DependencyObject>(associatedObject));

        foreach (DependencyObject item in this)
        {
            IXamlCompositionAnimationBase animation = (IXamlCompositionAnimationBase)item;
            animation.Start(associatedObject as UIElement);
        }
    }

    /// <summary>
    /// Detaches the collection of animations 
    /// </summary>
    public void Detach(DependencyObject associatedObject)
    {
        // Remove the WeakReference
        if (_associated.FirstOrDefault(f => f.TryGetTarget(out DependencyObject target) && target == associatedObject) 
            is { } weakReference)
            _associated.Remove(weakReference);

        // Stop all animations associated with the object
        foreach (DependencyObject item in this)
        {
            IXamlCompositionAnimationBase animation = (IXamlCompositionAnimationBase)item;
            animation.Stop(associatedObject as UIElement);
        }

        // Trim the associated list to remove any dead references
        Trim();
    }

    private void OnVectorChanged(IObservableVector<DependencyObject> sender, IVectorChangedEventArgs eventArgs)
    {
        Trim();

        var associated = _associated
               .Select(x => x.TryGetTarget(out DependencyObject target) ? target : null)
               .Where(x => x != null)
               .ToList();

        if (eventArgs.CollectionChange == CollectionChange.Reset)
        {
            // Stop all existing animations
            foreach (var item in associated)
            {
                foreach (IXamlCompositionAnimationBase behavior in this._oldCollection)
                    behavior.Stop(item as UIElement);
            }

            this._oldCollection.Clear();

            foreach (var item in associated)
            {
                foreach (IXamlCompositionAnimationBase behavior in this)
                    behavior.Start(item as UIElement);
            }

            foreach (IXamlCompositionAnimationBase newItem in this.OfType<IXamlCompositionAnimationBase>())
            {
                this._oldCollection.Add(newItem);
            }

            return;
        }

        int eventIndex = (int)eventArgs.Index;
        DependencyObject changedItem = this[eventIndex];

        switch (eventArgs.CollectionChange)
        {
            case CollectionChange.ItemInserted:

                this._oldCollection.Insert(eventIndex, changedItem as IXamlCompositionAnimationBase);

                if (changedItem is XamlCompositionAnimationBase x)
                {
                    x.AnimationUpdated -= Child_AnimationUpdated;
                    x.AnimationUpdated += Child_AnimationUpdated;

                    x.ParameterBindingUpdated -= Child_ParameterBindingUpdated;
                    x.ParameterBindingUpdated += Child_ParameterBindingUpdated;
                }

                foreach (var a in associated)
                    this.VerifiedAttach(changedItem, a);

                break;

            case CollectionChange.ItemChanged:
                IXamlCompositionAnimationBase oldItem = this._oldCollection[eventIndex];

                foreach (var a in associated)
                    oldItem.Stop(a as UIElement);

                Detach(oldItem);

                this._oldCollection[eventIndex] = changedItem as IXamlCompositionAnimationBase;

                foreach (var a in associated)
                    this.VerifiedAttach(changedItem, a);

                break;

            case CollectionChange.ItemRemoved:
                oldItem = this._oldCollection[eventIndex];

                foreach (var a in associated)
                    oldItem.Stop(a as UIElement);

                this._oldCollection.RemoveAt(eventIndex);
                Detach(oldItem);
                break;

            default:
                Debug.Assert(false, "Unsupported collection operation attempted.");
                break;
        }
    }


    private IXamlCompositionAnimationBase VerifiedAttach(DependencyObject item, DependencyObject associated)
    {
        IXamlCompositionAnimationBase animation = item as IXamlCompositionAnimationBase;
        if (animation == null)
        {
            throw new InvalidOperationException("NonAnimationAddedToAnimationCollection");
        }

        //if (this._oldCollection.Contains(animation))
        //{
        //    throw new InvalidOperationException("DuplicateAnimationInCollection");
        //}

        if (associated != null)
        {
            animation.Start(associated as UIElement);
        }

        return animation;
    }

    void Detach(IXamlCompositionAnimationBase i)
    {
        if (i is XamlCompositionAnimationBase x)
        {
            x.AnimationUpdated -= Child_AnimationUpdated;
            x.ParameterBindingUpdated -= Child_ParameterBindingUpdated;
        }
    }




    /* Respond to changes from child animations */

    void Child_ParameterBindingUpdated(object sender, ParameterUpdatedEventArgs e)
    {
        Handle(sender, e);
    }

    void Child_AnimationUpdated(object sender, AnimationUpdatedEventArgs e)
    {
        Handle(sender, e);
    }

    void Handle(object sender, IHandleableEvent e)
    {
        if (sender is XamlCompositionAnimationBase animation
            && e.Handled is false)
        {
            e.Handled = true;

            var associated = _associated
             .Select(x => x.TryGetTarget(out DependencyObject target) ? target : null)
             .Where(x => x != null)
             .OfType<UIElement>()
             .ToList();

            foreach (var item in associated)
            {
                if (VisualTreeHelper.GetParent(item) is null)
                    continue; // Skip if the item is not in the visual tree

                animation.Start(item);
            }
        }
    }
}



public sealed partial class AnimationParameterCollection : DependencyObjectCollection
{
    /// <summary>
    /// Fires when a child parameter value is updated and has a live bindingmode
    /// </summary>
    public event EventHandler<ParameterUpdatedEventArgs> BindingUpdated;

    // After a VectorChanged event we need to compare the current state of the collection
    // with the old collection so that we can call Detach on all removed items.
    private List<AnimationParameter> _oldCollection { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimationReferenceCollection"/> class.
    /// </summary>
    public AnimationParameterCollection()
    {
        this.VectorChanged += this.InternalVectorChanged;
    }

    public CompositionAnimation Target { get; private set; }

    public void SetTarget(CompositionAnimation target)
    {
        if (this.Target == target)
            return;

        // Detach old parameters
        if (this.Target is not null)
            foreach (AnimationParameter item in this._oldCollection)
                item.Detach();

        this.Target = target;

        // Attach new parameters
        foreach (AnimationParameter item in this)
            item.AttachTo(this.Target);

        this._oldCollection.Clear();
        this._oldCollection.AddRange(this.Cast<AnimationParameter>());
    }


    private void InternalVectorChanged(IObservableVector<DependencyObject> sender, IVectorChangedEventArgs eventArgs)
    {
        if (eventArgs.CollectionChange == CollectionChange.Reset)
        {
            // Stop all existing animations

            foreach (AnimationParameter a in this._oldCollection)
                a.Detach();

            this._oldCollection.Clear();

            foreach (AnimationParameter a in this)
            {
                a.AttachTo(Target);
                this._oldCollection.Add(a);
            }

            return;
        }

        int eventIndex = (int)eventArgs.Index;
        AnimationParameter changedItem = this[eventIndex] as AnimationParameter;

        switch (eventArgs.CollectionChange)
        {
            case CollectionChange.ItemInserted:

                this._oldCollection.Insert(eventIndex, changedItem);
                this.VerifiedAttach(changedItem);
                changedItem.AttachTo(Target);

                break;

            case CollectionChange.ItemChanged:
                AnimationParameter oldItem = this._oldCollection[eventIndex];

                oldItem.Detach();
                this._oldCollection[eventIndex] = changedItem;
                this.VerifiedAttach(changedItem);

                break;

            case CollectionChange.ItemRemoved:
                oldItem = this._oldCollection[eventIndex];
                oldItem.Detach();

                this._oldCollection.RemoveAt(eventIndex);
                break;

            default:
                Debug.Assert(false, "Unsupported collection operation attempted.");
                break;
        }
    }

    private AnimationParameter VerifiedAttach(DependencyObject item)
    {
        AnimationParameter animation = item as AnimationParameter;
        if (animation == null)
        {
            throw new InvalidOperationException("NonAnimationParameterAddedToAnimationParameterCollection");
        }

        animation.Updated -= Item_Updated;
        animation.Updated += Item_Updated;

        //if (this._oldCollection.Contains(animation))
        //{
        //    throw new InvalidOperationException("DuplicateAnimationParameterInCollection");
        //}

        return animation;
    }

    void Detach(AnimationParameter item)
    {
        item.Detach();
        item.Updated -= Item_Updated;
    }

    void Item_Updated(object sender, ParameterUpdatedEventArgs e)
    {
        this.BindingUpdated?.Invoke(this, e);
    }
}