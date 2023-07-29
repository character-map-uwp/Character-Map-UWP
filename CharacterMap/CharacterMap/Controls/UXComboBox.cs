using CharacterMap.Core;
using CharacterMap.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace CharacterMap.Controls
{
    public interface IThemeableControl
    {
        void UpdateTheme();
    }

    public class UXComboBoxItem : ComboBoxItem, IThemeableControl
    {
        public ThemeHelper _themer;
        public UXComboBoxItem()
        {
            Properties.SetStyleKey(this, "DefaultComboBoxItemStyle");
            _themer = new ThemeHelper(this);
        }

        public void UpdateTheme()
        {
            _themer.Update();
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _themer.Update();
        }
    }

    public class UXComboBox : ComboBox//, IThemeableControl
    {
        public string ToolTipMemberPath
        {
            get { return (string)GetValue(ToolTipMemberPathProperty); }
            set { SetValue(ToolTipMemberPathProperty, value); }
        }

        public static readonly DependencyProperty ToolTipMemberPathProperty =
            DependencyProperty.Register(nameof(ToolTipMemberPath), typeof(string), typeof(UXComboBox), new PropertyMetadata(null));


        public double ContentSpacing
        {
            get { return (double)GetValue(ContentSpacingProperty); }
            set { SetValue(ContentSpacingProperty, value); }
        }

        public static readonly DependencyProperty ContentSpacingProperty =
            DependencyProperty.Register("ContentSpacing", typeof(double), typeof(UXComboBox), new PropertyMetadata(0d));


        public Orientation ContentOrientation
        {
            get { return (Orientation)GetValue(ContentOrientationProperty); }
            set { SetValue(ContentOrientationProperty, value); }
        }

        public static readonly DependencyProperty ContentOrientationProperty =
            DependencyProperty.Register("ContentOrientation", typeof(Orientation), typeof(UXComboBox), new PropertyMetadata(Orientation.Vertical));


        public object PreContent
        {
            get { return (object)GetValue(PreContentProperty); }
            set { SetValue(PreContentProperty, value); }
        }

        public static readonly DependencyProperty PreContentProperty =
            DependencyProperty.Register("PreContent", typeof(object), typeof(UXComboBox), new PropertyMetadata(null, (d, e) =>
            {
                ((UXComboBox)d).UpdateContentStates();
            }));


        public object SecondaryContent
        {
            get { return (object)GetValue(SecondaryContentProperty); }
            set { SetValue(SecondaryContentProperty, value); }
        }

        public static readonly DependencyProperty SecondaryContentProperty =
            DependencyProperty.Register("SecondaryContent", typeof(object), typeof(UXComboBox), new PropertyMetadata(null, (d, e) =>
            {
                ((UXComboBox)d).UpdateContentStates();
            }));

        public ThemeHelper _themer;
        bool _isTemplateApplied = false;
        public UXComboBox()
        {
            //Properties.SetStyleKey(this, "DefaultThemeComboBoxStyle");
            //_themer = new ThemeHelper(this);
        }

        public void UpdateTheme()
        {
            //_themer.Update();
        }

        protected override void OnApplyTemplate()
        {
            _isTemplateApplied = true;

            base.OnApplyTemplate();
            //_themer.Update();

            UpdateContentStates();
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new UXComboBoxItem();
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);

            if (!string.IsNullOrEmpty(ToolTipMemberPath))
            {
                Binding b = new Binding { Source = item, Path = new PropertyPath(ToolTipMemberPath) };
                BindingOperations.SetBinding(element, ToolTipService.ToolTipProperty, b);
            }
        }

        void UpdateContentStates()
        {
            if (_isTemplateApplied is false)
                return;

            if (SecondaryContent is not null)
                VisualStateManager.GoToState(this, "PostContentState", false);
            else if (PreContent is not null)
                VisualStateManager.GoToState(this, "PreContentState", false);

        }
    }
}
