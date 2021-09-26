using CharacterMap.Core;
using CharacterMap.Helpers;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls
{
    public class UXButton : Button//, IThemeableControl
    {
        public bool IsHintVisible
        {
            get { return (bool)GetValue(IsHintVisibleProperty); }
            set { SetValue(IsHintVisibleProperty, value); }
        }

        public static readonly DependencyProperty IsHintVisibleProperty =
            DependencyProperty.Register(nameof(IsHintVisible), typeof(bool), typeof(UXButton), new PropertyMetadata(false, (d,e) =>
            {
                ((UXButton)d).UpdateHint();
            }));

        public bool IsLabelVisible
        {
            get { return (bool)GetValue(IsLabelVisibleProperty); }
            set { SetValue(IsLabelVisibleProperty, value); }
        }

        public static readonly DependencyProperty IsLabelVisibleProperty =
            DependencyProperty.Register(nameof(IsLabelVisible), typeof(bool), typeof(UXButton), new PropertyMetadata(false, (d, e) =>
            {
                ((UXButton)d).UpdateLabel();
            }));


        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(UXButton), new PropertyMetadata(null));


        bool _isTemplateApplied = false;

        //public ThemeHelper _themer;

        public UXButton()
        {
            //Properties.SetStyleKey(this, "DefaultThemeButtonStyle");
            //_themer = new ThemeHelper(this);
        }


        protected override void OnApplyTemplate()
        {
            _isTemplateApplied = true;

            base.OnApplyTemplate();
            //_themer.Update();

            UpdateHint(false);
            UpdateLabel(false);
        }

        private void UpdateHint(bool animate = true)
        {
            if (_isTemplateApplied)
                VisualStateManager.GoToState(this, IsHintVisible ? "HintVisible" : "HintHidden", animate);
        }

        private void UpdateLabel(bool animate = true)
        {
            if (_isTemplateApplied)
                VisualStateManager.GoToState(this, IsLabelVisible ? "LabelVisible" : "LabelHidden", animate);
        }

        public void UpdateTheme()
        {
            //_themer.Update();
        }
    }
}
