using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace CharacterMap.Helpers;

public static class CompositionExtensions
{
    public static UIElement EnableTranslation(this UIElement element, bool enable)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, enable);
        return element;
    }

    public static UIElement SetTranslation(this UIElement element, Vector3 value)
    {
        element.GetElementVisual().Properties.InsertVector3(CompositionFactory.TRANSLATION, value);
        return element;
    }

    public static Visual WithStandardTranslation(this Visual v)
    {
        return CompositionFactory.EnableStandardTranslation(v);
    }

    /// <summary>
    /// Returns a cached instance of the LinearEase function
    /// </summary>
    /// <param name="c"></param>
    /// <returns></returns>
    public static CompositionEasingFunction GetLinearEase(this Compositor c)
    {
        return c.GetCached("LINEAREASE", () => c.CreateLinearEasingFunction());
    }




    //------------------------------------------------------
    //
    // Expression Animation : SIZE LINKING
    //
    //------------------------------------------------------

    #region Linked Size Expression

    /*
     * An expression that matches the size of a visual to another.
     * Useful for keeping shadows etc. in size sync with their target.
     */

    static string LINKED_SIZE_EXPRESSION { get; } = $"{nameof(Visual)}.{nameof(Visual.Size)}";

    public static ExpressionAnimation CreateLinkedSizeExpression(FrameworkElement sourceElement)
    {
        return CreateLinkedSizeExpression(sourceElement.GetElementVisual());
    }


    public static ExpressionAnimation CreateLinkedSizeExpression(Visual sourceVisual)
    {
        return sourceVisual.CreateExpressionAnimation(nameof(Visual.Size))
                           .SetParameter(nameof(Visual), sourceVisual)
                           .SetExpression(LINKED_SIZE_EXPRESSION);
    }

    /// <summary>
    /// Starts an Expression Animation that links the size of <paramref name="sourceVisual"/> to the <paramref name="targetVisual"/>
    /// </summary>
    /// <param name="targetVisual">Element whose size you want to automatically change</param>
    /// <param name="sourceVisual"></param>
    /// <returns></returns>
    public static T LinkSize<T>(this T targetVisual, Visual sourceVisual) where T : Visual
    {
        targetVisual.StartAnimation(CreateLinkedSizeExpression(sourceVisual));
        return targetVisual;
    }

    public static T LinkShapeSize<T>(this T targetVisual, Visual sourceVisual) where T : CompositionGeometry
    {
        targetVisual.StartAnimation(CreateLinkedSizeExpression(sourceVisual));
        return targetVisual;
    }

    /// <summary>
    /// Starts an Expression Animation that links the size of <paramref name="element"/> to the <paramref name="targetVisual"/>
    /// </summary>
    /// <param name="targetVisual">Element whose size you want to automatically change</param>
    /// <param name="element">Element whose size will change <paramref name="targetVisual"/>s size</param>
    /// <returns></returns>
    public static T LinkSize<T>(this T targetVisual, FrameworkElement element) where T : Visual
    {
        targetVisual.StartAnimation(CreateLinkedSizeExpression(element.GetElementVisual()));
        return targetVisual;
    }

    #endregion
}
