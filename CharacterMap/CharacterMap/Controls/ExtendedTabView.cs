using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;

namespace CharacterMap.Controls;

[DependencyProperty<bool>("IsExpanded")]
public partial class ExtendedTabView : TabView
{
    TabViewListView _tabListView = null;

    public ExtendedTabView()
    {

    }

    protected override void OnApplyTemplate()
    {
        if (_tabListView is not null)
        {
            _tabListView.ContainerContentChanging -= Tlv_ContainerContentChanging;
            _tabListView = null;
        }

        base.OnApplyTemplate();

        if (this.GetTemplateChild("TabListView") is TabViewListView { } tlv)
        {
            _tabListView = tlv;

            tlv.ChoosingItemContainer -= Tlv_ChoosingItemContainer;
            tlv.ChoosingItemContainer += Tlv_ChoosingItemContainer;
            tlv.ContainerContentChanging -= Tlv_ContainerContentChanging;
            tlv.ContainerContentChanging += Tlv_ContainerContentChanging;
        }

        if (this.GetTemplateChild("OtherButton") is Button b)
        {
            b.Click -= B_Click;
            b.Click += B_Click;

            void B_Click(object sender, RoutedEventArgs e)
            {
                IsExpanded = !IsExpanded;
            }
        }
    }

    partial void OnIsExpandedChanged(bool o, bool n)
    {
        string state = n ? "ExpandedState" : "NotExpandedState";
        VisualStateManager.GoToState(this, state, true);

        if (_tabListView is not null)
        {
            foreach (var item in _tabListView.GetFirstLevelDescendantsOfType<TabViewItem>())
                VisualStateManager.GoToState(item, state, true);
        }
    }

    private void Tlv_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is not { } container) return;

        string state = IsExpanded ? "ExpandedState" : "NotExpandedState";
        VisualStateManager.GoToState(args.ItemContainer, state, true);
    }

    private void Tlv_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
    {
        if (args.ItemContainer is not { } container) return;
    }
}
