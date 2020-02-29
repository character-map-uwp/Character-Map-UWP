using CharacterMap.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace CharacterMap.Styles
{
    public sealed partial class Controls : ResourceDictionary
    {
        public Controls()
        {
            this.InitializeComponent();
        }

        private void ContentRoot_Loaded(object sender, RoutedEventArgs e)
        {
            Animation.SetStandardReposition(sender, e);
        }

        private void PaneRoot_Loaded(object sender, RoutedEventArgs e)
        {
            var f = (UIElement)sender;
            ElementCompositionPreview.SetIsTranslationEnabled(f, true);
            Visual v = ElementCompositionPreview.GetElementVisual(f);

            var o = v.Compositor.CreateVector3KeyFrameAnimation();
            o.Target = "Translation";
            o.InsertExpressionKeyFrame(0, "this.StartingValue");
            o.InsertKeyFrame(1, new System.Numerics.Vector3(-256, 0, 0));
            o.Duration = TimeSpan.FromSeconds(Animation.DefaultOffsetDuration);

            ElementCompositionPreview.SetImplicitHideAnimation(f, o);

            var o2 = v.Compositor.CreateVector3KeyFrameAnimation();
            o2.Target = "Translation";
            o2.InsertExpressionKeyFrame(0, "this.StartingValue");
            o2.InsertKeyFrame(1, new System.Numerics.Vector3(0, 0, 0));
            o2.Duration = TimeSpan.FromSeconds(Animation.DefaultOffsetDuration);

            ElementCompositionPreview.SetImplicitShowAnimation(f, o2);
        }
    }
}
