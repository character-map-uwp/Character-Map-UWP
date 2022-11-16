using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace CharacterMap.Views
{
    public sealed partial class CalligraphyView : ViewBase
    {
        public CalligraphyViewModel ViewModel { get; }

        private InkStrokeContainer _container => Ink.InkPresenter.StrokeContainer;

        public CalligraphyView(CharacterRenderingOptions options)
        {
            this.InitializeComponent();
            ViewModel = new CalligraphyViewModel(options);
            this.Loaded += OnLoaded;

            ResourceHelper.GoToThemeState(this);
            LeakTrackingService.Register(this);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "NormalState", false);
            VisualStateManager.GoToState(this, "OverlayState", false);

            TitleBarHelper.SetTitle(Presenter.Title);

            Ink.InkPresenter.StrokesCollected -= InkPresenter_StrokesCollected;
            Ink.InkPresenter.StrokesCollected += InkPresenter_StrokesCollected;

            Ink.InkPresenter.StrokesErased -= InkPresenter_StrokesErased;
            Ink.InkPresenter.StrokesErased += InkPresenter_StrokesErased;

            // Pre-create element visuals to ensure animations run
            // properly when requested later
            PresentationRoot.GetElementVisual();
            Guide.GetElementVisual();
            CanvasContainer.GetElementVisual();
        }

        private void InkPresenter_StrokesErased(InkPresenter sender, InkStrokesErasedEventArgs args)
        {
            UpdateStrokes();
        }

        private void InkPresenter_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            if (args.Strokes.Count > 0)
                ViewModel.HasStrokes = true;
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
                await ViewModel.AddToHistoryAsync(_container);

                // Scroll to the end of the list view to ensure the ConnectedAnimation
                // can play properly
                HistoryList.ScrollIntoView(HistoryList.Items.Last());

                _container.Clear();
                UpdateStrokes();
            }
        }

        private void HistoryList_ItemClick(object sender, ItemClickEventArgs e)
        {
            _container.Clear();

            if (e.ClickedItem is CalligraphyHistoryItem h)
            {
                if (ViewModel.AllowAnimation)
                    Inker.Opacity = 0;

                _container.AddStrokes(h.GetStrokes());
                //GoToOverlay();
                TryAnimateToInkCanvas(e);
            }

            UpdateStrokes();
        }



        private void Reset()
        {
            /// Clear the Ink Canvas and reset back to the 
            /// default calligraphy pen

            _container.Clear();

            // This needs to be done on the dispatcher or the 
            // InkButton will not go into the correct VisualState
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                Toolbar.ActiveTool = calligraphyPen;
            });

            UpdateStrokes();
        }

        private void HistoryList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            TryAnimateInkIntoHistory(args);
        }

        public void UndoLastStroke()
        {
            IReadOnlyList<InkStroke> strokes = _container.GetStrokes();
            if (strokes.Count > 0)
            {
                strokes[strokes.Count - 1].Selected = true;
                _container.DeleteSelected();

                UpdateStrokes();
            }
        }

        private void UpdateStrokes()
        {
            ViewModel.HasStrokes = _container.GetStrokes().Count > 0;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement f && f.DataContext is CalligraphyHistoryItem item)
            {
                ViewModel.Histories.Remove(item);
            }
        }


        /* ANIMATION HELPERS */

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




        /* VISUAL STATE HELPERS */

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
