using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls
{
    public class ExtendedSplitView : SplitView
    {
        public bool EnableAnimation
        {
            get { return (bool)GetValue(EnableAnimationProperty); }
            set { SetValue(EnableAnimationProperty, value); }
        }

        public static readonly DependencyProperty EnableAnimationProperty =
            DependencyProperty.Register(nameof(EnableAnimation), typeof(bool), typeof(ExtendedSplitView), new PropertyMetadata(true, (d, e) =>
            {
                if (d is ExtendedSplitView s)
                    s.UpdateAnimationStates();
            }));

        public ExtendedSplitView()
        {
            this.DefaultStyleKey = typeof(ExtendedSplitView);
            this.Loaded += ExtendedSplitView_Loaded;
            this.Unloaded += ExtendedSplitView_Unloaded;
        }

        FrameworkElement _contentRoot = null;
        FrameworkElement _paneRoot = null;

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _contentRoot = this.GetTemplateChild("ContentRoot") as FrameworkElement;
            _paneRoot = this.GetTemplateChild("PaneRoot") as FrameworkElement;

            UpdateAnimationStates();
        }

        private void ExtendedSplitView_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateAnimationStates();
        }
        private void ExtendedSplitView_Unloaded(object sender, RoutedEventArgs e)
        {
            UpdateAnimationStates(false);
        }

        private void UpdateAnimationStates(bool allow = true)
        {
            if (_contentRoot is not null)
                Core.Properties.SetUseStandardReposition(_contentRoot, allow && EnableAnimation);

            if (_paneRoot is not null)
                Styles.Controls.SetEnableSlideOut(_paneRoot, allow && EnableAnimation);
        }
    }
}
