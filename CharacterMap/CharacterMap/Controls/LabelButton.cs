using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

[DependencyProperty("IconContent")]
public sealed partial class LabelButton : Button
{
    static IReadOnlyList<string> _previewStates { get; } = ["PointerOver", "Pressed"];

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
                    .SetDuration(ResourceHelper.AllowAnimation ? 0.4 : 0.001)
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
                    .SetParameter("Child", _iconPresenter.GetElementVisual())
                    .SetDuration(ResourceHelper.AllowAnimation ? -1 : 0.001));
        }
    }
}
