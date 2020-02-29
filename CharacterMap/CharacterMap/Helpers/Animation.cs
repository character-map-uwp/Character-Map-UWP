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
    public static class Animation
    {
        public const double DefaultOffsetDuration = 0.325;

        private static Dictionary<Compositor, Vector3KeyFrameAnimation> _defaultOffsetAnimations { get; } 
            = new Dictionary<Compositor, Vector3KeyFrameAnimation>();

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
    }
}
