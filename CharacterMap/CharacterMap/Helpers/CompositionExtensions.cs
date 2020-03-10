using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
