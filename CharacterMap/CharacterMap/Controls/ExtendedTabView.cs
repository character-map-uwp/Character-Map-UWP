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
[DependencyProperty<Visibility>("ExpansionButtonVisibility", Visibility.Collapsed)]
public partial class ExtendedTabView : TabView
{
    TabViewListView _tabListView = null;

    public event EventHandler<bool> IsExpandedChanged;

    public ExtendedTabView()
    {
        this.DefaultStyleKey = typeof(ExtendedTabView);
    }

    protected override void OnApplyTemplate()
    {
        if (_tabListView is not null)
        {
            _tabListView.ContainerContentChanging -= Tlv_ContainerContentChanging;
            _tabListView.SelectionChanged -= Tlv_SelectionChanged;
            _tabListView = null;
        }

        base.OnApplyTemplate();

        if (this.GetTemplateChild("TabListView") is TabViewListView { } tlv)
        {
            _tabListView = tlv;

            //tlv.ChoosingItemContainer -= Tlv_ChoosingItemContainer;
            //tlv.ChoosingItemContainer += Tlv_ChoosingItemContainer;
            tlv.ContainerContentChanging -= Tlv_ContainerContentChanging;
            tlv.ContainerContentChanging += Tlv_ContainerContentChanging;
            tlv.SelectionChanged -= Tlv_SelectionChanged;
            tlv.SelectionChanged += Tlv_SelectionChanged;
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
        VisualStateManager.GoToState(this, state, ResourceHelper.AllowAnimation);

        if (_tabListView is not null)
        {
            foreach (var item in _tabListView.GetFirstLevelDescendantsOfType<TabViewItem>())
                VisualStateManager.GoToState(item, state, ResourceHelper.AllowAnimation);
        }

        IsExpandedChanged?.Invoke(this, n);
    }

    private void Tlv_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateTabSelectionStates();
        this.GetDispatcherQueue().Enqueue(UpdateTabSelectionStates);
    }

    private void UpdateTabSelectionStates()
    {
        if (_tabListView is null)
            return;

        foreach (TabViewItem item in _tabListView.GetFirstLevelDescendantsOfType<TabViewItem>())
            SyncSelectionState(item);
    }

    private void Tlv_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.ItemContainer is not { } container)
            return;

        string state = IsExpanded ? "ExpandedState" : "NotExpandedState";
        VisualStateManager.GoToState(container, state, ResourceHelper.AllowAnimation);

        if (container is TabViewItem tvi)
            SyncSelectionState(tvi);
    }

    private void Tvi_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TabViewItem tvi)
        {
            tvi.Loaded -= Tvi_Loaded;
            SyncSelectionState(tvi, hookLoaded: false);
        }
    }

    private void SyncSelectionState(TabViewItem tvi, bool hookLoaded = true)
    {
        VisualStateGroup group = tvi.GetVisualStateGroup("CommonStates");
        string current = group?.CurrentState?.Name;
        bool isSelectedState = current is "Selected" or "PointerOverSelected" or "PressedSelected";

        if (tvi.IsSelected)
        {
            if (!isSelectedState && !VisualStateManager.GoToState(tvi, "Selected", false) && hookLoaded)
            {
                tvi.Loaded -= Tvi_Loaded;
                tvi.Loaded += Tvi_Loaded;
            }
        }
        else if (isSelectedState && !VisualStateManager.GoToState(tvi, "Normal", false) && hookLoaded)
        {
            tvi.Loaded -= Tvi_Loaded;
            tvi.Loaded += Tvi_Loaded;
        }
    }

    //private void Tlv_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
    //{
    //    if (args.ItemContainer is not { } container) return;
    //}
}
