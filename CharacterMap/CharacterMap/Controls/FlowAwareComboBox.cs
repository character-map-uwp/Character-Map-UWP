using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls
{
    public class FlowAwareComboBox : ComboBox
    {
        public event EventHandler<FlowDirection> DetectedFlowDirectionChanged;

        public bool IsColorFontEnabled
        {
            get { return (bool)GetValue(IsColorFontEnabledProperty); }
            set { SetValue(IsColorFontEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsColorFontEnabledProperty =
            DependencyProperty.Register(nameof(IsColorFontEnabled), typeof(bool), typeof(FlowAwareComboBox), new PropertyMetadata(true));

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

        private Binding _colorFontBinding { get; }

        private ContentPresenter _presenter;

        private long _presenterReg = 0;

        public FlowAwareComboBox()
        {
            _colorFontBinding = new() { Source = this, Path = new(nameof(IsColorFontEnabled)) };
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (this.GetTemplateChild("ContentPresenter") is ContentPresenter p)
            {
                _presenter = p;

                // We want the ContentPresenter (whose content will be a TextBlock) to respect the
                // IsColorFontEnabled property. There's no easy way to bind this, so we have handle two cases:
                // 1: First load of the ContentPresenter. For this we need to listen to Size changed and wait
                //    for it to create it's content.
                // 2: When the content changes after the first load. For this we listen to the Content DP

                p.SizeChanged -= P_SizeChanged;
                p.SizeChanged += P_SizeChanged;

                if (_presenterReg != 0)
                    p.UnregisterPropertyChangedCallback(ContentPresenter.ContentProperty, _presenterReg);
                _presenterReg = p.RegisterPropertyChangedCallback(ContentPresenter.ContentProperty, ContentChanged);
            }
            if (this.GetTemplateChild("EditableText") is TextBox box)
            {
                box.SetBinding(TextBox.IsColorFontEnabledProperty, _colorFontBinding);
                box.TextChanged -= Box_TextChanged;
                box.TextChanged += Box_TextChanged;
            }
        }

        private void P_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ((FrameworkElement)sender).SizeChanged -= P_SizeChanged;
            if (sender is ContentPresenter p && VisualTreeHelper.GetChild(p, 0) is TextBlock t)
                t.SetBinding(TextBlock.IsColorFontEnabledProperty, _colorFontBinding);
        }

        private void ContentChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (sender is ContentPresenter p && p.Content is TextBlock t)
                t.SetBinding(TextBlock.IsColorFontEnabledProperty, _colorFontBinding);
        }

        private void Box_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox box && box.Text is not null && box.Text.Length > 0)
            {
                try
                {
                    // We detect LTR or RTL by checking where the first character is
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
