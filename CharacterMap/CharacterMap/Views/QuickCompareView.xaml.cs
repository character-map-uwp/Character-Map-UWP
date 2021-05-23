using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using CharacterMapCX.Controls;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using static System.Net.Mime.MediaTypeNames;

namespace CharacterMap.Views
{
    public class ExtendedRepeater : ItemsRepeater
    { 
        public void InvalidateLayout()
        {
            this.InvalidateViewport();
        }
    }

    public sealed partial class QuickCompareView : ViewBase
    {
        public QuickCompareViewModel ViewModel { get; }

        public QuickCompareView() : this(false) { }

        public QuickCompareView(bool isQuickCompare)
        {
            this.InitializeComponent();

            ViewModel = new QuickCompareViewModel(isQuickCompare);
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            this.DataContext = this;
            this.Loaded += QuickCompareView_Loaded;
            this.Unloaded += QuickCompareView_Unloaded;

            if (isQuickCompare)
            {
                VisualStateManager.GoToState(this, QuickCompareState.Name, false);
            }

            LeakTrackingService.Register(this);
        }

        private void QuickCompareView_Loaded(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, NormalState.Name, false);
            TitleBarHelper.SetTitle(CompareFontsTitle.Text);
        }

        private void QuickCompareView_Unloaded(object sender, RoutedEventArgs e)
        {
            ViewModel?.Deactivated();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.FontList))
            {
                if (ViewStates.CurrentState != NormalState)
                    GoToNormalState();

                CompositionFactory.PlayEntrance(Repeater, 0, 80, 0);

                // ItemsRepeater is a bit rubbish, needs to be nudged back into life.
                // If we scroll straight to zero, we can often end up with a blank screen
                // until the user scrolls. So we need to manually hack in a scroll ourselves.
                //ListingScroller.ChangeView(null, 2, null, true);
                //_ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                //{
                //    await Task.Delay(16);
                //    ListingScroller?.ChangeView(null, 0, null, false);
                //});
            }
            else if (e.PropertyName == nameof(ViewModel.SelectedFont))
            {
                if (ViewModel.SelectedFont is null)
                {
                    GoToNormalState();
                }
                else
                {
                    DetailsFontTitle.Text = "";
                    VisualStateManager.GoToState(this, DetailsState.Name, true);
                }
            }
            else if (e.PropertyName == nameof(ViewModel.Text))
            {
                UpdateText(ViewModel.Text);
            }
        }

        private void GoToNormalState()
        {
            // Repeater metrics may be out of date. Update.
            UpdateText(ViewModel.Text, Repeater.Realize().ItemsPanelRoot);
            UpdateFontSize(FontSizeSlider.Value, Repeater.Realize().ItemsPanelRoot);
            VisualStateManager.GoToState(this, NormalState.Name, true);
        }

        private void MenuFlyout_Opening(object sender, object e)
        {
            // Handles forming the flyout when opening the main FontFilter 
            // drop down menu.
            if (sender is MenuFlyout menu)
            {
                // Reset to default menu
                while (menu.Items.Count > 8)
                    menu.Items.RemoveAt(8);

                // force menu width to match the source button
                foreach (var sep in menu.Items.OfType<MenuFlyoutSeparator>())
                    sep.MinWidth = FontListFilter.ActualWidth;

                // add users collections 
                if (ViewModel.FontCollections.Items.Count > 0)
                {
                    menu.Items.Add(new MenuFlyoutSeparator());
                    foreach (var item in ViewModel.FontCollections.Items)
                    {
                        var m = new MenuFlyoutItem { DataContext = item, Text = item.Name, FontSize = 16 };
                        m.Click += (s, a) =>
                        {
                            if (m.DataContext is UserFontCollection u)
                            {

                                ViewModel.SelectedCollection = u;
                            }
                        };
                        menu.Items.Add(m);
                    }
                }

                VariableOption.SetVisible(FontFinder.HasVariableFonts);

                if (!FontFinder.HasAppxFonts && !FontFinder.HasRemoteFonts)
                {
                    FontSourceSeperator.Visibility = CloudFontsOption.Visibility = AppxOption.Visibility = Visibility.Collapsed;
                }
                else
                {
                    FontSourceSeperator.Visibility = Visibility.Visible;
                    CloudFontsOption.SetVisible(FontFinder.HasRemoteFonts);
                    AppxOption.SetVisible(FontFinder.HasAppxFonts);
                }

                static void SetCommand(MenuFlyoutItemBase b, ICommand c)
                {
                    b.FontSize = 16;
                    if (b is MenuFlyoutSubItem i)
                    {
                        foreach (var child in i.Items)
                            SetCommand(child, c);
                    }
                    else if (b is MenuFlyoutItem m)
                        m.Command = c;
                }

                foreach (var item in menu.Items)
                    SetCommand(item, ViewModel.FilterCommand);
            }
        }

        private void Repeater_EffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            Debug.WriteLine($"{args.EffectiveViewport}");
        }




        /*
         * ElementName Bindings don't work inside ItemsRepeater, so to change
         * preview Text & FontSize we need to manually update all TextBlocks
         */

        bool IsDetailsView => ViewStates.CurrentState == DetailsState;

        void UpdateText(string text, FrameworkElement root = null)
        {
            FrameworkElement target = root ?? (IsDetailsView ? DetailsRepeater : Repeater.Realize().ItemsPanelRoot);
            if (target == null)
                return;

            XamlBindingHelper.SuspendRendering(target);
            foreach (var g in target.GetFirstLevelDescendantsOfType<Panel>().Where(g => g.ActualOffset.X >= 0))
                SetText(g, text);
            XamlBindingHelper.ResumeRendering(target);
        }

        void UpdateFontSize(double size, FrameworkElement root = null)
        {
            FrameworkElement target = root ?? (IsDetailsView ? DetailsRepeater : Repeater.Realize().ItemsPanelRoot);
            if (target == null)
                return;

            XamlBindingHelper.SuspendRendering(target);
            foreach (var g in target.GetFirstLevelDescendantsOfType<Panel>().Where(g => g.ActualOffset.X >= 0))
                SetFontSize(g, size);
            XamlBindingHelper.ResumeRendering(target);
        }

        private void FontSizeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (Repeater == null)
                return;

            double v = e.NewValue;
            UpdateFontSize(v);
        }

        void SetText(Panel root, string text)
        {
            var t = root.Children.Skip(1).FirstOrDefault();
            if (t is TextBlock tb)
                tb.Text = text;
            else if (t is DirectText d)
                d.Text = text;
        }

        void SetFontSize(Panel root, double size)
        {
            var t = root.Children.Skip(1).FirstOrDefault();
            if (t is TextBlock tb)
                tb.FontSize = size;
            else if (t is DirectText d)
                d.FontSize = size;
        }

        private void DetailsRepeater_ElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
        {
            // Unload x:Bind content
            if (args.Element is FrameworkElement f && f.Tag is FrameworkElement t)
            {
                UnloadObject(t);
                f.Tag = null;
            }
        }

        private void Repeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if (args.Element is Button b && b.Content is Panel g)
            {
                SetText(g, InputText.Text);
                SetFontSize(g, FontSizeSlider.Value);
            }
            else if (args.Element is Panel g1)
            {
                SetText(g1, InputText.Text);
                SetFontSize(g1, FontSizeSlider.Value);
            }
        }

        private void ItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Content is InstalledFont font && Repeater is ListViewBase list)
            {
                var item = list.ContainerFromItem(font);
                var title = item.GetFirstDescendantOfType<TextBlock>();
                ConnectedAnimationService.GetForCurrentView().DefaultDuration = TimeSpan.FromSeconds(0.7);
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("Title", title);

                ViewModel.SelectedFont = font;
            }
            else if (sender is Button bn && bn.Content is CharacterRenderingOptions o && ViewModel.IsQuickCompare)
            {
                ContextFlyout.SetItemsDataContext(o);
                ContextFlyout.ShowAt(bn);
            }
        }

        private void ItemContextRequested(UIElement sender, Windows.UI.Xaml.Input.ContextRequestedEventArgs args)
        {
            if (ViewModel.IsQuickCompare && sender is Button b)
            {
                ContextFlyout.SetItemsDataContext(b.Content);
                ContextFlyout.ShowAt(b);
            }
        }

        private void OpenWindow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item 
                && item.DataContext is CharacterRenderingOptions o
                && FontFinder.Fonts.FirstOrDefault(f => f.Variants.Contains(o.Variant)) is InstalledFont font)
            {
                _ = FontMapView.CreateNewViewForFontAsync(font, null, o);
            }
        }

        private void Remove_Clicked(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.DataContext is CharacterRenderingOptions o)
                ViewModel.QuickFonts.Remove(o);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectedFont = null;
        }

        private void GridView_Click(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, GridLayoutState.Name, true);
        }

        private void ListView_Click(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, StackLayoutState.Name, true);
        }

        private  void ViewStates_CurrentStateChanging(object sender, VisualStateChangedEventArgs e)
        {
            if (e.NewState == DetailsState)
            {
                var ani = ConnectedAnimationService.GetForCurrentView().GetAnimation("Title");
                //ani.Configuration = new BasicConnectedAnimationConfiguration();
                //var c = this.GetElementVisual().Compositor;
                //var offset = c.CreateScalarKeyFrameAnimation();

                ////CubicBezierEasingFunction ease = c.CreateCubicBezierEasingFunction(
                ////  new Vector2(0.95f, 0.05f),
                ////  new Vector2(0.79f, 0.04f));

                //CubicBezierEasingFunction easeOut = c.CreateCubicBezierEasingFunction(
                //new Vector2(0.13f, 1.0f),
                //new Vector2(0.49f, 1.0f));

                //offset.InsertExpressionKeyFrame(0.0f, "StartingValue");
                ////offset.InsertExpressionKeyFrame(0.2f, "StartingValue");
                //offset.InsertExpressionKeyFrame(1, "FinalValue", easeOut);
                //offset.Duration = TimeSpan.FromSeconds(0.6);
                //offset.DelayTime = TimeSpan.FromSeconds(0.15);

                //ani.SetAnimationComponent(ConnectedAnimationComponent.OffsetX, offset);
                //ani.SetAnimationComponent(ConnectedAnimationComponent.OffsetY, offset);
                //ani.SetAnimationComponent(ConnectedAnimationComponent.Scale, offset);
                //ani.SetAnimationComponent(ConnectedAnimationComponent.CrossFade, offset);

                DetailsFontTitle.Text = ViewModel.SelectedFont.Name;
                ani.TryStart(DetailsTitleContainer);//, new List<UIElement> { DetailsViewContent });
            }
        }

        private void Repeater_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var g = args.ItemContainer.GetFirstDescendantOfType<Panel>();
            if (!args.InRecycleQueue)
            {
                g.DataContext = args.Item;
                SetText(g, ViewModel.Text);
                SetFontSize(g, FontSizeSlider.Value);
            }

            if (ResourceHelper.AppSettings.AllowExpensiveAnimations)
            {
                if (args.InRecycleQueue)
                {
                    CompositionFactory.PokeUIElementZIndex(args.ItemContainer);
                }
                else
                {
                    var v = ElementCompositionPreview.GetElementVisual(args.ItemContainer);
                    v.ImplicitAnimations = CompositionFactory.GetRepositionCollection(v.Compositor);
                }
            }
        }
    }

    public partial class QuickCompareView
    {
        public static async Task<WindowInformation> CreateWindowAsync(bool isQuickCompare)
        {
            // 1. If QuickCompare (rather than FontCompare), return the existing window
            //    if we have one. (QuickCompare is ALWAYS single window)
            if (isQuickCompare && QuickCompareViewModel.QuickCompareWindow is not null)
                return QuickCompareViewModel.QuickCompareWindow;

            static void CreateView(bool isQuickCompare)
            {
                QuickCompareView view = new(isQuickCompare);
                Window.Current.Content = view;
                Window.Current.Activate();
            }

            var view = await WindowService.CreateViewAsync(() => CreateView(isQuickCompare), false);
            await WindowService.TrySwitchToWindowAsync(view, false);

            if(isQuickCompare)
                QuickCompareViewModel.QuickCompareWindow = view;
            
            return view;
        }

        public static async Task AddAsync(CharacterRenderingOptions options)
        {
            // 1. Ensure QuickCompare Window exists
            var window = await CreateWindowAsync(true);

            // 2. Add selected font to QuickCompare
            await QuickCompareViewModel.QuickCompareWindow.CoreView.Dispatcher.ExecuteAsync(() =>
            {
                WeakReferenceMessenger.Default.Send(options, nameof(QuickCompareViewModel));
            });

            // 3. Try switch to view.
            //    Task.Delay is required as TrySwitchToWindow may fail.
            await Task.Delay(64);
            await WindowService.TrySwitchToWindowAsync(QuickCompareViewModel.QuickCompareWindow, false);
        }
    }
}
