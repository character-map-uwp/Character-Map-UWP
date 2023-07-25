using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls
{
    public class FlowAwareComboBox : ComboBox
    {
        public event EventHandler<FlowDirection> DetectedFlowDirectionChanged;

        public FlowDirection DetectedFlowDirection
        {
            get { return (FlowDirection)GetValue(DetectedFlowDirectionProperty); }
            set { SetValue(DetectedFlowDirectionProperty, value); }
        }

        public static readonly DependencyProperty DetectedFlowDirectionProperty =
            DependencyProperty.Register(nameof(DetectedFlowDirection), typeof(FlowDirection), typeof(FlowAwareComboBox), new PropertyMetadata(FlowDirection.LeftToRight, (d,e) =>
            {
                ((FlowAwareComboBox)d).DetectedFlowDirectionChanged?.Invoke(d, (FlowDirection)e.NewValue);
            }));

        private FrameworkElement _presenter;

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _presenter = this.GetTemplateChild("ContentPresenter") as FrameworkElement;
            if (this.GetTemplateChild("EditableText") is TextBox box)
            {
                box.TextChanged -= Box_TextChanged;
                box.TextChanged += Box_TextChanged;
            }
        }

        private void Box_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox box && box.Text is not null && box.Text.Length > 0)
            {
                try
                {
                    var rect = box.GetRectFromCharacterIndex(0, false);

                    // Note: Probably we can check if rect.X == 0, but I haven't properly tested it.
                    //       The current logic will fail with a small width TextBox with small fonts,
                    //       but that's not a concern for our current use cases.
                    DetectedFlowDirection = rect.X <= this.ActualWidth / 4.0 ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;

                    if (_presenter is not null)
                        _presenter.FlowDirection = DetectedFlowDirection;
                }
                catch
                {
                    // Don't care
                }
            }
        }
    }
}
