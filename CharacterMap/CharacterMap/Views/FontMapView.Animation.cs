using CharacterMap.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace CharacterMap.Views
{
    public partial class FontMapView
    {
        private Random _r { get; } = new Random();

        List<FrameworkElement> GetTypeRampAnimationTargets()
        {
            if (TypeRampRoot is null)
            {
                this.FindName(nameof(TypeRampRoot));

                // Calling measure will force an ItemsControl to populate its
                // ItemsPanel with realized children.
                VariableAxis.Measure(CharGrid.DesiredSize);
                TypeRampList.Measure(CharGrid.DesiredSize);
            }

            var items = TypeRampList.ItemsPanelRoot.Children.OfType<FrameworkElement>();

            if (VariableAxis.ItemsPanelRoot is not null)
                items = items.Concat(VariableAxis.ItemsPanelRoot.Children.OfType<FrameworkElement>());

            return items.Append(TypeRampInputRow).OrderBy(g => Guid.NewGuid()).ToList();
        }

        private Storyboard CreateHidePreview(bool setSpan = true, bool targetContent = true)
        {
            Storyboard sb = new Storyboard();

            FrameworkElement target = targetContent ? PreviewGridContent : PreviewGrid;
            FrameworkElement splitter = targetContent ? SplitterContainerContent : SplitterContainer;

            if (setSpan)
            {
                sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(CharGridRoot, TargetProperty.GridColumnSpan)
                    .AddKeyFrame(0, 3);
            }

            sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(target, TargetProperty.CompositeTransform.TranslateX)
                .AddKeyFrame(0.075, 0)
                .AddKeyFrame(0.4, target.RenderSize.Width, KeySplines.CompositionDefault);

            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(target, TargetProperty.Visibility)
                .AddKeyFrame(0.4, Visibility.Collapsed);

            sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(splitter, TargetProperty.CompositeTransform.TranslateX)
                .AddKeyFrame(0.075, 0)
                .AddKeyFrame(0.4, target.RenderSize.Width + splitter.RenderSize.Width, KeySplines.CompositionDefault);

            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(splitter, TargetProperty.Visibility)
                .AddKeyFrame(0.4, Visibility.Collapsed);

            return sb;
        }

        private Storyboard CreateShowPreview(double offset = 0, bool targetContent = true)
        {
            FrameworkElement target = targetContent ? PreviewGridContent : PreviewGrid;
            FrameworkElement splitter = targetContent ? SplitterContainerContent : SplitterContainer;

            Storyboard sb = new Storyboard();

            if (!targetContent)
            {
                sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(PreviewColumn, nameof(PreviewColumn.Width))
                    .AddKeyFrame(0, new GridLength(ViewModel.Settings.LastColumnWidth));

                sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(PreviewColumn, nameof(PreviewColumn.MinWidth))
                    .AddKeyFrame(0, 150);
            }

            sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(target, TargetProperty.CompositeTransform.TranslateX)
               .AddKeyFrame(0, target.RenderSize.Width)
               .If(offset != 0, t => t.AddKeyFrame(offset, target.RenderSize.Width))
               .AddKeyFrame(offset + CompositionFactory.DefaultOffsetDuration, 0, KeySplines.CompositionDefault);

            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(target, TargetProperty.Visibility)
                .If(offset != 0, t => t.AddKeyFrame(0, Visibility.Collapsed))
                .AddKeyFrame(offset, Visibility.Visible);

            //sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(splitter, nameof(PreviewColumn.Width))
            //    .AddKeyFrame(0, new GridLength(10));

            sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(splitter, TargetProperty.CompositeTransform.TranslateX)
                .AddKeyFrame(0, target.RenderSize.Width + splitter.RenderSize.Width)
                .If(offset != 0, t => t.AddKeyFrame(offset, target.RenderSize.Width + splitter.RenderSize.Width))
                .AddKeyFrame(offset + CompositionFactory.DefaultOffsetDuration, 0, KeySplines.CompositionDefault);

            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(splitter, TargetProperty.Visibility)
                .If(offset != 0, t => t.AddKeyFrame(0, Visibility.Collapsed))
                .AddKeyFrame(offset, Visibility.Visible);

            return sb;
        }

        private Storyboard CreateHideCopyPane(bool targetContent = false)
        {
            Storyboard sb = new Storyboard();

            if (CopySequenceRoot != null)
            {
                FrameworkElement target = targetContent ? CopySequenceContent : CopySequenceRoot;

                if (!targetContent)
                {
                     sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(CharGrid, TargetProperty.GridRowSpan)
                                        .AddKeyFrame(0, 3);
                }

                sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(target, TargetProperty.CompositeTransform.TranslateY)
                    .AddKeyFrame(CompositionFactory.DefaultOffsetDuration, CopySequenceRoot.RenderSize.Height, KeySplines.CompositionDefault);

                sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(target, TargetProperty.Visibility)
                    .AddKeyFrame(0, Visibility.Visible)
                    .AddKeyFrame(CompositionFactory.DefaultOffsetDuration, Visibility.Collapsed);
            }

            return sb;
        }

        private Storyboard CreateShowCopyPane()
        {
            Storyboard sb = new Storyboard();

            if (CopySequenceRoot != null)
            {
                sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(CharGrid, TargetProperty.GridRowSpan)
                    .AddKeyFrame(0, 3);

                sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(CopySequenceRoot, TargetProperty.CompositeTransform.TranslateY)
                    .AddKeyFrame(0, CopySequenceRoot.RenderSize.Height)
                    .AddKeyFrame(CompositionFactory.DefaultOffsetDuration, 0, KeySplines.CompositionDefault);

                sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(CopySequenceRoot, TargetProperty.Visibility)
                    .AddKeyFrame(0, Visibility.Visible);
            }

            return sb;
        }

        public void UpdateGridToRampTransition()
        {
            // 0. Realise items
            if (CharGrid.ItemsPanelRoot is null)
            {
                CharGrid.Measure(CharGrid.DesiredSize);
                if (CharGrid.ItemsPanelRoot is null)
                    return;
            }

            // 1.0. Get all the items we'll be animating
            List<FrameworkElement> childs = CharGrid.ItemsPanelRoot.Children
                .OfType<FrameworkElement>()
                .Where(c => c.IsInViewport(CharGrid))
                .OrderBy(c => Guid.NewGuid()).ToList();

            List<FrameworkElement> toChilds = GetTypeRampAnimationTargets();

            // 1.1. Default animation configuration
            double fromDepth = -400;
            double toDepth = 300;

            TimeSpan outStagger = TimeSpan.FromMilliseconds(250d / childs.Count);
            TimeSpan startOffset = TimeSpan.FromSeconds(0);
            TimeSpan staggerTime = TimeSpan.FromMilliseconds(40);
            TimeSpan duration = TimeSpan.FromMilliseconds(400);
            TimeSpan durationOpacityOut = TimeSpan.FromMilliseconds(150);
            TimeSpan durationOpacityIn = TimeSpan.FromMilliseconds(300);

            // 1.2. Build base storyboard and assign it as the VisualState transition
            Storyboard sb = new Storyboard();
            GridToRampTransition.Storyboard = sb;



            /* --- START ANIMATION BUILDING --- */

            // 2. Animate out PreviewGrid, Splitter, CopyPane
            sb.Children.Add(CreateHidePreview(false));
            sb.Children.Add(CreateHideCopyPane(true));

            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(CharGridHeader, TargetProperty.GridColumnSpan)
                .AddKeyFrame(0, 3);

            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(MoreOptionsButton, nameof(MoreOptionsButton.Margin))
                .AddKeyFrame(0, new Thickness(0, 0, -8, 0));

            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(SearchBox, TargetProperty.Visibility)
                .AddKeyFrame(0, Visibility.Visible);

            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(CharacterFilterButton, TargetProperty.Visibility)
               .AddKeyFrame(0, Visibility.Collapsed);

            sb.CreateTimeline<DoubleAnimation>(SearchBox, TargetProperty.CompositeTransform.TranslateY)
                .To(-80)
                .SetDuration(0.4)
                .SetEase(new BackEase { Amplitude = 0.8, EasingMode = EasingMode.EaseIn});

            // 3. Animate out Character Grid items
            foreach (var item in childs)
            {
                // 3.0. Get the item and it's opacity 
                var trans = item.GetCompositeTransform3D();
                trans.CenterX = item.RenderSize.Width / 2d;
                trans.CenterY = item.RenderSize.Height / 2d;

                // 3.2. Animate the opacity
                sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(item, TargetProperty.Opacity)
                    .AddKeyFrame(TimeSpan.Zero, item.Opacity)
                    .AddKeyFrame(startOffset, item.Opacity)
                    .AddKeyFrame(startOffset.Add(durationOpacityOut), 0, KeySplines.DepthZoomOpacity);

                // 3.3. Animate the 3D depth translation
                if (toDepth != 0)
                {
                    sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(item, TargetProperty.CompositeTransform3D.TranslateZ)
                        .AddKeyFrame(TimeSpan.Zero, trans.TranslateZ)
                        .AddKeyFrame(startOffset, trans.TranslateZ)
                        .AddKeyFrame(startOffset.Add(duration), toDepth, KeySplines.EntranceTheme);
                }

                // 3.4. Add randomised 3D rotation
                //var d = 60;
                //sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(item, TargetProperty.CompositeTransform3D.RotationX)
                //       .AddKeyFrame(TimeSpan.Zero, trans.RotationX)
                //       .AddKeyFrame(startOffset, trans.RotationX)
                //       .AddKeyFrame(startOffset.Add(duration), _r.Next(-d, d), KeySplines.EntranceTheme);

                //sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(item, TargetProperty.CompositeTransform3D.RotationY)
                //      .AddKeyFrame(TimeSpan.Zero, trans.RotationY)
                //      .AddKeyFrame(startOffset, trans.RotationY)
                //      .AddKeyFrame(startOffset.Add(duration), _r.Next(-d, d), KeySplines.EntranceTheme);

                //sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(item, TargetProperty.CompositeTransform3D.RotationZ)
                //      .AddKeyFrame(TimeSpan.Zero, trans.RotationZ)
                //      .AddKeyFrame(startOffset, trans.RotationZ)
                //      .AddKeyFrame(startOffset.Add(duration), _r.Next(-d, d), KeySplines.EntranceTheme);

                // 3.5. Increment start offset
                startOffset = startOffset.Add(outStagger);
            }

            // 4. Adjust visibility on CharGrid/TypeRamp in the middle of the animation
            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(CharGridRoot, TargetProperty.Visibility)
                .AddKeyFrame(startOffset.Add(duration.Multiply(0.8)), Visibility.Collapsed);

            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(TypeRampRoot, TargetProperty.Visibility)
                .AddKeyFrame(0, Visibility.Collapsed)
                .AddKeyFrame(startOffset, Visibility.Visible);

            // 5. Animate in TypeRamp items
            foreach (var item in toChilds)
            {
                // 5.1. Set rotation centre points
                var trans = item.GetCompositeTransform3D();
                trans.CenterX = item.RenderSize.Width / 2d;
                trans.CenterY = item.RenderSize.Height / 2d;

                // 5.2. Animate the opacity
                sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(item, TargetProperty.Opacity)
                    .AddKeyFrame(TimeSpan.Zero, 0)
                    .AddKeyFrame(startOffset, 0)
                    .AddKeyFrame(startOffset.Add(durationOpacityIn), 1, KeySplines.DepthZoomOpacity);

                // 5.3. Animate the 3D depth translation
                if (fromDepth != 0)
                {
                    sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(item, TargetProperty.CompositeTransform3D.TranslateZ)
                        .AddKeyFrame(TimeSpan.Zero, fromDepth)
                        .AddKeyFrame(startOffset, fromDepth)
                        .AddKeyFrame(startOffset.Add(duration), 0, KeySplines.EntranceTheme);
                }

                // 5.4. Increment start offset
                startOffset = startOffset.Add(staggerTime);
            }
        }

        public void UpdateRampToGridTransition()
        {
            if (TypeRampList == null)
                return;

            if (CharGrid.ItemsPanelRoot is null)
                CharGrid.Measure(CharGrid.DesiredSize);

            // 1. Build base storyboard and assign it as the 
            //    VisualState transition
            Storyboard sb = new Storyboard();
            //sb.Children.Add(GridToTypeBase);
            RampToGridTransition.Storyboard = sb;

            var toChilds = CharGrid.Realize().ItemsPanelRoot.Children
                .OfType<FrameworkElement>()
                .Where(c => c.IsInViewport(CharGrid))
                .OrderBy(c => Guid.NewGuid()).ToList();

            var childs = GetTypeRampAnimationTargets();

            var fromDepth = 400;
            double toDepth = -300;

            TimeSpan charStagger = TimeSpan.FromMilliseconds(250d / toChilds.Count);

            TimeSpan startOffset = TimeSpan.FromSeconds(0);
            TimeSpan staggerTime = TimeSpan.FromMilliseconds(40);
            TimeSpan duration = TimeSpan.FromMilliseconds(250);
            TimeSpan durationOpacityOut = TimeSpan.FromMilliseconds(150);
            TimeSpan durationOpacityIn = TimeSpan.FromMilliseconds(300);

            foreach (var item in childs)
            {
                // 3.0. Get the item and it's opacity 
                var trans = item.GetCompositeTransform3D();

                trans.RotationX = trans.RotationY = trans.RotationZ = 0;

                // 3.2. Animate the opacity
                sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(item, TargetProperty.Opacity)
                    .AddKeyFrame(TimeSpan.Zero, item.Opacity)
                    .AddKeyFrame(startOffset, item.Opacity)
                    .AddKeyFrame(startOffset.Add(durationOpacityOut), 0, KeySplines.DepthZoomOpacity);

                // 3.3. Animate the 3D depth translation
                if (toDepth != 0)
                {
                    sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(item, TargetProperty.CompositeTransform3D.TranslateZ)
                        .AddKeyFrame(TimeSpan.Zero, trans.TranslateZ)
                        .AddKeyFrame(startOffset, trans.TranslateZ)
                        .AddKeyFrame(startOffset.Add(duration), toDepth, KeySplines.EntranceTheme);
                }

                // 3.4. Increment start offset
                startOffset = startOffset.Add(staggerTime);
            }

            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(SearchBox, TargetProperty.Visibility)
                .AddKeyFrame(0, Visibility.Visible);

            sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(SearchBox, TargetProperty.CompositeTransform.TranslateY)
                .AddKeyFrame(0, -80)
                .AddKeyFrame(startOffset, -80)
                .AddKeyFrame(startOffset.TotalSeconds + 0.4, 0, new BackEase { Amplitude = 0.8, EasingMode = EasingMode.EaseOut });

            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(TypeRampRoot, TargetProperty.Visibility)
                .AddKeyFrame(startOffset.Add(duration.Multiply(0.8)), Visibility.Collapsed);

            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(CharGridRoot, TargetProperty.Visibility)
                .AddKeyFrame(0, Visibility.Collapsed)
                .AddKeyFrame(startOffset, Visibility.Visible);


            // X. Show PreviewGrid, Splitter, CopyPane
            sb.Children.Add(CreateShowPreview(startOffset.TotalSeconds));

            if (CopySequenceRoot is not null)
            {
                sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(CopySequenceContent, TargetProperty.CompositeTransform.TranslateY)
                .AddKeyFrame(0, CopySequenceContent.RenderSize.Height)
                .AddKeyFrame(startOffset, CopySequenceContent.RenderSize.Height)
                .AddKeyFrame(startOffset.TotalSeconds + CompositionFactory.DefaultOffsetDuration, 0, KeySplines.CompositionDefault);

                sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(CopySequenceContent, TargetProperty.Visibility)
                    .AddKeyFrame(0, Visibility.Collapsed)
                    .AddKeyFrame(startOffset, Visibility.Visible);
            }


            // 3. Now let's build the storyboard!
            foreach (var item in toChilds)
            {
                // 3.0. Get the item and it's opacity 
                //Double _originalOpacity = _opacitys[i];
                item.GetCompositeTransform3D();

                // 3.1. Check AddedDelay
                //startOffset = startOffset.Add(Properties.GetAddedDelay(item));

                // 3.2. Animate the opacity
                sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(item, TargetProperty.Opacity)
                    .AddKeyFrame(TimeSpan.Zero, 0)
                    .AddKeyFrame(startOffset, 0)
                    .AddKeyFrame(startOffset.Add(durationOpacityIn), 1, KeySplines.DepthZoomOpacity);

                // 3.3. Animate the 3D depth translation
                if (fromDepth != 0)
                {
                    sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(item, TargetProperty.CompositeTransform3D.TranslateZ)
                        .AddKeyFrame(TimeSpan.Zero, fromDepth)
                        .AddKeyFrame(startOffset, fromDepth)
                        .AddKeyFrame(startOffset.Add(duration), 0, KeySplines.EntranceTheme);
                }

                //var x = _r.Next(-90, 90);
                //sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(item, TargetProperty.CompositeTransform3D.RotationX)
                //     .AddKeyFrame(TimeSpan.Zero, x)
                //     .AddKeyFrame(startOffset, x)
                //     .AddKeyFrame(startOffset.Add(duration), 0, KeySplines.EntranceTheme);

                //var y = _r.Next(-90, 90);
                //sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(item, TargetProperty.CompositeTransform3D.RotationY)
                //     .AddKeyFrame(TimeSpan.Zero, y)
                //     .AddKeyFrame(startOffset, y)
                //     .AddKeyFrame(startOffset.Add(duration), 0, KeySplines.EntranceTheme);

                //var z = _r.Next(-90, 90);
                //sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(item, TargetProperty.CompositeTransform3D.RotationZ)
                //     .AddKeyFrame(TimeSpan.Zero, z)
                //     .AddKeyFrame(startOffset, z)
                //     .AddKeyFrame(startOffset.Add(duration), 0, KeySplines.EntranceTheme);

                // 3.4. Increment start offset
                startOffset = startOffset.Add(charStagger);
            }
        }
    }
}
