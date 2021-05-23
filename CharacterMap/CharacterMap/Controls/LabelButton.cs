using CharacterMap.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls
{
    public sealed class LabelButton : Button
    {
        public object IconContent
        {
            get { return (object)GetValue(IconContentProperty); }
            set { SetValue(IconContentProperty, value); }
        }

        public static readonly DependencyProperty IconContentProperty =
            DependencyProperty.Register("IconContent", typeof(object), typeof(LabelButton), new PropertyMetadata(null));

        static IReadOnlyList<string> _previewStates { get; } = new List<String> { "PointerOver", "Pressed" };

        FrameworkElement _iconPresenter = null;

        public LabelButton()
        {
            this.DefaultStyleKey = typeof(LabelButton);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _iconPresenter = this.GetTemplateChild("Border") as FrameworkElement;

            if (_iconPresenter != null)
            {
                var vis = this.GetElementVisual();

                var clip = vis.Compositor.CreateInsetClip();
                vis.Clip = clip;

                string exp = "Parent.Size.X - Child.Size.X";
                var ani = vis.CreateExpressionAnimation(nameof(InsetClip.RightInset))
                               .SetExpression(exp)
                               .SetParameter("Parent", vis)
                               .SetParameter("Child", _iconPresenter.GetElementVisual());

                clip.StartAnimation(ani);
            }

            if (this.GetTemplateChild("Root") is FrameworkElement _rootGrid)
            {
                var groups = VisualStateManager.GetVisualStateGroups(_rootGrid);
                var common = groups?.FirstOrDefault(s => s.Name.Equals("CommonStates"));
                if (common != null)
                {
                    common.CurrentStateChanging -= Common_CurrentStateChanging;
                    common.CurrentStateChanging += Common_CurrentStateChanging;
                }
            }
        }



        private void Common_CurrentStateChanging(object sender, VisualStateChangedEventArgs e)
        {
            if (_previewStates.Contains(e.NewState.Name) &&
               (e.OldState == null || !_previewStates.Contains(e.OldState.Name)))
            {
                var vis = this.GetElementVisual();
                vis.Clip.StartAnimation(
                    vis.CreateScalarKeyFrameAnimation(nameof(InsetClip.RightInset))
                        .AddKeyFrame(1, 0)
                        .SetDuration(0.4)
                        .SetParameter("Parent", vis)
                        .SetParameter("Child", _iconPresenter.GetElementVisual()));
            }
            else if (!_previewStates.Contains(e.NewState.Name) &&
                (_previewStates.Contains(e.OldState.Name)))
            {
                var vis = this.GetElementVisual();
                vis.Clip.StartAnimation(
                    vis.CreateScalarKeyFrameAnimation(nameof(InsetClip.RightInset))
                        .AddKeyFrame(1, "Parent.Size.X - Child.Size.X")
                        .SetParameter("Parent", this.GetElementVisual())
                        .SetParameter("Child", _iconPresenter.GetElementVisual()));
            }
        }
    }
}
