using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace CharacterMap.Helpers
{
    public static class CompositionExtensions
    {
        public static Visual GetElementVisual(this UIElement element) => ElementCompositionPreview.GetElementVisual(element);


        public static T SetImplicitAnimation<T>(this T composition, string path, ICompositionAnimationBase animation)
            where T : CompositionObject
        {
            if (composition.ImplicitAnimations == null)
            {
                composition.ImplicitAnimations = composition.Compositor.CreateImplicitAnimationCollection();
            }

            composition.ImplicitAnimations[path] = animation;
            return composition;
        }

        public static FrameworkElement SetImplicitAnimation(this FrameworkElement element, string path, ICompositionAnimationBase animation)
        {
            CompositionObject composition = ElementCompositionPreview.GetElementVisual(element);

            if (composition.ImplicitAnimations == null)
            {
                composition.ImplicitAnimations = composition.Compositor.CreateImplicitAnimationCollection();
            }

            composition.ImplicitAnimations[path] = animation;
            return element;
        }

        public static void SetShowAnimation(this UIElement element, ICompositionAnimationBase animation)
        {
            ElementCompositionPreview.SetImplicitShowAnimation(element, animation);
        }

        public static void SetHideAnimation(this UIElement element, ICompositionAnimationBase animation)
        {
            ElementCompositionPreview.SetImplicitHideAnimation(element, animation);
        }

        public static UIElement EnableTranslation(this UIElement element, bool enable)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(element, enable);
            return element;
        }

        public static CubicBezierEasingFunction CreateEntranceEasingFunction(this Compositor c)
        {
            return c.CreateCubicBezierEasingFunction(new Vector2(.1f, .9f), new Vector2(.2f, 1));
        }

        public static CompositionAnimationGroup CreateAnimationGroup(this Compositor c, params CompositionAnimation[] animations)
        {
            var group = c.CreateAnimationGroup();
            foreach (var a in animations)
                group.Add(a);
            return group;
        }
    }
}
