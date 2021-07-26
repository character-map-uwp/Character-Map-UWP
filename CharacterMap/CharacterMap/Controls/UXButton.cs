using CharacterMap.Core;
using CharacterMap.Helpers;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls
{
    public class UXButton : Button, IThemeableControl
    {
        public ThemeHelper _themer;
        public UXButton()
        {
            Properties.SetStyleKey(this, "DefaultThemeButtonStyle");
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
}
