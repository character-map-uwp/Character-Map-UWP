using Windows.UI.Composition;

namespace CharacterMap.Helpers;

/// <summary>
/// Extension of <see cref="Composition"/>, required for generic extension
/// methods as NaturalMotionAnimation and CompositionAnimation have different
/// base classes and generic methods can't handle it
/// </summary>
internal static class CompositionNaturalMotion
{
    /// <summary>
    /// Sets the delay time in seconds
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="animation"></param>
    /// <param name="delayTime">Delay Time in seconds</param>
    /// <returns></returns>
    public static T SetDelayTime<T>(this T animation, double delayTime)
        where T : NaturalMotionAnimation
    {
        animation.DelayTime = TimeSpan.FromSeconds(delayTime);
        return animation;
    }

    public static T SetDelayTime<T>(this T animation, TimeSpan delayTime)
        where T : NaturalMotionAnimation
    {
        animation.DelayTime = delayTime;
        return animation;
    }

    public static T SetDelayBehavior<T>(this T animation, AnimationDelayBehavior behavior)
        where T : NaturalMotionAnimation
    {
        animation.DelayBehavior = behavior;
        return animation;
    }

    #region SetDelay

    /// <summary>
    /// Sets the delay time in seconds
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="animation"></param>
    /// <param name="delayTime">Delay Time in seconds</param>
    /// <returns></returns>
    public static T SetDelay<T>(this T animation, double delayTime, AnimationDelayBehavior behavior)
        where T : NaturalMotionAnimation
    {
        animation.DelayTime = TimeSpan.FromSeconds(delayTime);
        animation.DelayBehavior = behavior;
        return animation;
    }

    public static T SetDelay<T>(this T animation, TimeSpan delayTime, AnimationDelayBehavior behavior)
        where T : NaturalMotionAnimation
    {
        animation.DelayBehavior = behavior;
        animation.DelayTime = delayTime;
        return animation;
    }

    #endregion


    #region SetDampingRatio

    public static SpringVector3NaturalMotionAnimation SetDampingRatio(this SpringVector3NaturalMotionAnimation animation, float dampingRatio)
    {
        animation.DampingRatio = dampingRatio;
        return animation;
    }

    #endregion


    #region SetPeriod

    public static SpringVector3NaturalMotionAnimation SetPeriod(
        this SpringVector3NaturalMotionAnimation animation, double duration)
    {
        if (duration >= 0)
            return SetPeriod(animation, TimeSpan.FromSeconds(duration));
        else
            return animation;
    }

    public static SpringVector3NaturalMotionAnimation SetPeriod(
        this SpringVector3NaturalMotionAnimation animation, TimeSpan duration)
    {
        animation.Period = duration;
        return animation;
    }

    #endregion


    #region SetInitialValue

    public static T SetInitialValue<T>(this T animation, float x, float y, float z) where T : Vector3NaturalMotionAnimation
    {
        animation.InitialValue = new(x, y, z);
        return animation;
    }

    public static T SetInitialValue<T>(this T animation, Vector3? value) where T : Vector3NaturalMotionAnimation
    {
        animation.InitialValue = value;
        return animation;
    }

    #endregion


    #region SetFinalValue

    public static T SetFinalValue<T>(this T animation, float x, float y, float z) where T : Vector3NaturalMotionAnimation
    {
        animation.FinalValue = new(x, y, z);
        return animation;
    }

    public static T SetFinalValue<T>(this T animation, Vector3? value) where T : Vector3NaturalMotionAnimation
    {
        animation.FinalValue = value;
        return animation;
    }

    #endregion
}