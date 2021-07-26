using CharacterMap.Core;
using CharacterMap.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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

    public class UXComboBox : ComboBox, IThemeableControl
    {
        public ThemeHelper _themer;
        public UXComboBox()
        {
            Properties.SetStyleKey(this, "DefaultThemeComboBoxStyle");
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

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new UXComboBoxItem();
        }
    }
}
