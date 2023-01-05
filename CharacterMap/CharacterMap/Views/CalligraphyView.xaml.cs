using CharacterMap.Controls;
using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace CharacterMap.Views
{
    public sealed partial class CalligraphyView : ViewBase, IInAppNotificationPresenter
    {
        public CalligraphyViewModel ViewModel { get; }

        private InkStrokeContainer _container => Ink.InkPresenter.StrokeContainer;

        public CalligraphyView(CharacterRenderingOptions options)
        {
            this.InitializeComponent();
            ViewModel = new CalligraphyViewModel(options);

            if (DesignMode)
                return;

            ContentRoot.Opacity = 0;
        }

        protected override void OnLoaded(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "NormalState", false);
            VisualStateManager.GoToState(this, "OverlayState", false);

            TitleBarHelper.SetTitle(Presenter.Title);

            Ink.InkPresenter.StrokesCollected -= InkPresenter_StrokesCollected;
            Ink.InkPresenter.StrokesCollected += InkPresenter_StrokesCollected;

            Ink.InkPresenter.StrokesErased -= InkPresenter_StrokesErased;
            Ink.InkPresenter.StrokesErased += InkPresenter_StrokesErased;

            Register<AppNotificationMessage>(OnNotificationMessage);

            // Pre-create element visuals to ensure animations run
            // properly when requested later
            PresentationRoot.GetElementVisual();
            Guide.GetElementVisual();
            CanvasContainer.GetElementVisual();

            AnimateIn();
        }

        /// <summary>
        /// Clear the InkCanvas and reset back to the 
        /// default calligraphy pen
        /// </summary>
        private void Reset()
        {
            ViewModel.Clear();

            // This needs to be done on the dispatcher or the 
            // InkButton will not go into the correct VisualState
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                Toolbar.ActiveTool = calligraphyPen;
            });
        }

        void FocusCanvas()
        {
            // Ensures keyboard shortcuts work (as focus by default falls on the TextBox
            // which will steal keyboard shortcut input)
            if (InputBox.ContainsFocus() || FontSizeSlider.ContainsFocus())
            {
                AddButton.Focus(FocusState.Programmatic);
                AddButton.RemoveFocusEngagement();
            }
        }

        private void InkPresenter_StrokesErased(InkPresenter sender, InkStrokesErasedEventArgs args)
        {
            ViewModel.OnStrokesErased(sender.StrokeContainer, args.Strokes);
            FocusCanvas();
        }

        private void InkPresenter_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            ViewModel.OnStrokeDrawn(_container, args.Strokes);
            FocusCanvas();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (ViewStates.CurrentState == OverlayState)
                GoToSideBySide();
            else
                GoToOverlay();
        }

        private async void AddHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_container.GetStrokes().Count > 0)
            {
                TryPrepareHistoryAnimation();

                await ViewModel.AddToHistoryAsync();

                // Scroll to the end of the list view to ensure the ConnectedAnimation
                // can play properly
                HistoryList.ScrollIntoView(HistoryList.Items.Last());

                ViewModel.Clear();
            }
        }

        private void HistoryList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            TryAnimateInkIntoHistory(args);
        }

        private void HistoryList_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.Clear();

            if (e.ClickedItem is CalligraphyHistoryItem h)
            {
                if (ViewModel.AllowAnimation)
                    Inker.Opacity = 0;

                // Restore History Item
                _container.AddStrokes(h.GetStrokes());
                ViewModel.FontSize = h.FontSize;
                ViewModel.Text = h.Text;

                // Animate if required
                TryAnimateToInkCanvas(e);
            }

            ViewModel.InkManager.UpdateControls();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement f && f.DataContext is CalligraphyHistoryItem item)
            {
                ViewModel.Histories.Remove(item);
            }
        }

        private void Flyout_Opening(object sender, object e)
        {
            if (sender is Flyout flyout)
            {
                if (flyout.Content is CharacterPicker p)
                    p.CharacterSelected -= P_CharacterSelected;

                p = new CharacterPicker(ViewModel.Options);
                p.CharacterSelected += P_CharacterSelected;
                flyout.Content = p;
            }
        }

        private void P_CharacterSelected(object sender, Character e)
        {
            ViewModel.Text += e.Char;
        }

        private void SaveAsSVG(object sender, RoutedEventArgs e)
        {
            _ = SaveAsync(_container.GetStrokes(), ExportFormat.Svg, _container.BoundingRect);
        }

        private void SaveAsPNG(object sender, RoutedEventArgs e)
        {
            _ = SaveAsync(_container.GetStrokes(), ExportFormat.Png, _container.BoundingRect);
        }

        private void SaveHistoryAsSVG(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement f && f.DataContext is CalligraphyHistoryItem item)
                _ = SaveAsync(item.GetStrokes(), ExportFormat.Svg, item.Bounds);
        }

        private void SaveHistoryAsPNG(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement f && f.DataContext is CalligraphyHistoryItem item)
                _ = SaveAsync(item.GetStrokes(), ExportFormat.Png, item.Bounds);
        }

        private Task SaveAsync(IReadOnlyList<InkStroke> strokes, ExportFormat format, Rect bounds)
        {
            return ViewModel.SaveAsync(strokes, format, ShimCanvas, bounds);
        }




        /* Notification Helpers */

        public InAppNotification GetNotifier()
        {
            if (NotificationRoot == null)
                this.FindName(nameof(NotificationRoot));

            return DefaultNotification;
        }

        void OnNotificationMessage(AppNotificationMessage msg)
        {
            if (!Dispatcher.HasThreadAccess)
                return;

            InAppNotificationHelper.OnMessage(this, msg);
        }




        /* ANIMATION HELPERS */

        #region Animation

        /// <summary>
        /// Animates the page in on first load
        /// </summary>
        private void AnimateIn()
        {
            ContentRoot.Opacity = 1;
            if (ResourceHelper.AllowAnimation is false)
                return;

            int s = 100;
            int o = 110;

            // Title
            CompositionFactory.PlayEntrance(Presenter.GetTitleElement(), s + 30, o);

            // First Row
            CompositionFactory.PlayEntrance(PickerButton, s + 113, o);
            CompositionFactory.PlayEntrance(InputContainer, s + 113, o);
            CompositionFactory.PlayEntrance(SliderContainer, s + 113, o);

            // Second Row
            CompositionFactory.PlayEntrance(ContentGrid, s + 200, o);

            // Third Row
            CompositionFactory.PlayEntrance(PresentationRoot, s + 300, o);
        }

        ConnectedAnimation _addHistoryAnim;

        private void TryPrepareHistoryAnimation()
        {
            if (ViewModel.AllowAnimation)
                _addHistoryAnim = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("ink", Ink);
        }

        private void TryAnimateInkIntoHistory(ContainerContentChangingEventArgs args)
        {
            if (_addHistoryAnim is not null && args.Item == ViewModel.Histories.Last())
            {
                args.ItemContainer.Opacity = 0;
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    args.ItemContainer.Opacity = 1;
                    HistoryList.ScrollIntoView(ViewModel.Histories.Last());
                    _addHistoryAnim.TryStart(args.ItemContainer);
                    _addHistoryAnim = null;
                });
            }
        }

        private void TryAnimateToInkCanvas(ItemClickEventArgs e)
        {
            if (ViewModel.AllowAnimation)
            {
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate(
                    "ToInk", HistoryList.ContainerFromItem(e.ClickedItem).GetFirstDescendantOfType<Image>())
                        .TryStart(Inker);

                Inker.Opacity = 1;
            }
        }

        #endregion




        /* VISUAL STATE HELPERS */

        #region State Helpers

        private void GoToOverlay()
        {
            VisualStateManager.GoToState(this, nameof(OverlayState), false);

            var gv = Guide.EnableCompositionTranslation().GetElementVisual();
            var iv = CanvasContainer.EnableCompositionTranslation().GetElementVisual();

            if (ViewModel.AllowAnimation)
            {
                var scale = CompositionFactory.CreateScaleAnimation(gv.Compositor);
                gv.WithStandardTranslation().SetImplicitAnimation("Scale", scale);
                iv.WithStandardTranslation().SetImplicitAnimation("Scale", scale);
            }

            gv.SetTranslation(0, 0, 0);
            iv.SetTranslation(0, 0, 0);

            gv.Scale = new System.Numerics.Vector3(1f);
            iv.Scale = new System.Numerics.Vector3(1f);
        }

        async void GoToSideBySide()
        {
            // 0. Go to state & disable swap button
            //    We need to disable because we're doing funky animation things
            SwapButton.IsEnabled = false;

            VisualStateManager.GoToState(this, nameof(SideBySideState), false);

            // 1. Prepare visuals
            var v = PresentationRoot.GetElementVisual();
            var gv = Guide.EnableCompositionTranslation().GetElementVisual();
            var iv = CanvasContainer.EnableCompositionTranslation().GetElementVisual();

            // 2. Prepare implicit animations
            if (ViewModel.AllowAnimation)
            {
                var scale = CompositionFactory.CreateScaleAnimation(gv.Compositor);
                gv.WithStandardTranslation().SetImplicitAnimation("Scale", scale);
                iv.WithStandardTranslation().SetImplicitAnimation("Scale", scale);
            }

            CompositionFactory.StartCentering(gv);
            CompositionFactory.StartCentering(iv);

            // 3. Set scale & translation. If implicit animations applied, these
            //    will cause animations to start playing
            gv.SetTranslation(v.Size.X / -4f, 0, 0);
            iv.SetTranslation(v.Size.X / 4f, 0, 0);

            gv.Scale = new System.Numerics.Vector3(0.5f, 0.5f, 1f);
            iv.Scale = new System.Numerics.Vector3(0.5f, 0.5f, 1f);

            if (ViewModel.AllowAnimation)
                await Task.Delay((int)(CompositionFactory.DefaultOffsetDuration * 1000) + 32);

            // 4. Now enable expression animation for layout. This will stomp over our
            //    translation implicit animations (which is why we do this after the delay)
            gv.StartAnimation(
                gv.CreateExpressionAnimation(CompositionFactory.TRANSLATION)
                .SetExpression("Vector3(-(v.Size.X / 4f), 0, 0)")
                .SetParameter("v", v));

            iv.StartAnimation(
                iv.CreateExpressionAnimation(CompositionFactory.TRANSLATION)
                .SetExpression("Vector3((v.Size.X / 4f), 0, 0)")
                .SetParameter("v", v));

            // 5. Re-enable button to swap
            SwapButton.IsEnabled = true;
        }




        #endregion

        
    }

    public partial class CalligraphyView
    {
        public static async Task<WindowInformation> CreateWindowAsync(CharacterRenderingOptions options, string text = null)
        {
            static void CreateView(CharacterRenderingOptions v, string t = null)
            {
                CalligraphyView view = new(v);
                view.ViewModel.Text = String.IsNullOrWhiteSpace(t) ? "Hello" : t;
                Window.Current.Content = view;
                Window.Current.Activate();
            }

            var view = await WindowService.CreateViewAsync(() => CreateView(options, text), false);
            await WindowService.TrySwitchToWindowAsync(view, false);

            return view;
        }
    }

    public class CalligraphicPen : InkToolbarCustomPen
    {
        public CalligraphicPen() { }

        protected override InkDrawingAttributes CreateInkDrawingAttributesCore(Brush brush, double width)
        {
            return new InkDrawingAttributes()
            {
                IgnorePressure = false,
                PenTip = PenTipShape.Circle,
                Size = new (width, 2.0f * width),
                Color = (brush as SolidColorBrush)?.Color ?? Colors.Black,
                PenTipTransform = Matrix3x2.CreateRotation((float)(Math.PI * 45d / 180d))
            };
        }
    }
}
