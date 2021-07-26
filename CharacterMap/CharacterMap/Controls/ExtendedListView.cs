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
    public class ExtendedListViewItem : ListViewItem, IThemeableControl
    {
        public ThemeHelper _themer;
        public ExtendedListViewItem() : base()
        {
            Properties.SetStyleKey(this, "DefaultThemeListViewItemStyle");
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

    public class ExtendedListView : ListView
    {
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ExtendedListViewItem();
        }
    }
}
