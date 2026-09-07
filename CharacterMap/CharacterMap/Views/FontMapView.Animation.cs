using System.Transactions;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace CharacterMap.Views;

public partial class FontMapView
{
    class StoryboardBuilderArgs()
    {
        public Storyboard Storyboard { get; } = new();
        public TimeSpan CurrentOffset { get; set; } = TimeSpan.Zero;
        public double FromDepth { get; set; } = -400;
        public double ToDepth { get; set; } = 300;
        public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(250);
        public TimeSpan DurationOpacityIn { get; set; } = TimeSpan.FromMilliseconds(300);
    }

    private Random _r { get; } = new Random();

    public void PlayFontChanged(bool withHeader = true, bool isTabChange = false)
    {
        /* Create the animation that is played upon changing font */
        if (ResourceHelper.AllowAnimation)
        {
            //if (this.GetFirstAncestorOfType<MainPage>() is { FontsTabBar: { TabItemsSource: ObservableCollection<FontItem> items } tabBar })
            //{
            //    var idx = items.IndexOf(items.FirstOrDefault(i => i == ViewModel.SelectedFont));
            //    if (idx < 0 || tabBar.ContainerFromIndex(idx) is not FrameworkElement container)
            //        return;

            //    if (container.GetBoundingRect(this) is not Rect bounds) return;
            //    var center = bounds.Left + (bounds.Width / 2f);
            //    var ratio = center / this.ActualWidth;
            //    CompositionFactory.PlayTabEntrace(this, ratio);
            //    return;
            //}

            int offset = 0;
            if (withHeader)
            {
                offset = 83;
                CompositionFactory.PlayEntrance(CharGridHeader, 83);
            }

            if (ViewModel.DisplayMode == FontDisplayMode.CharacterMapState)
            {
                if (!withHeader)
                {
                    CompositionFactory.PlayEntrance(CharGrid, offset);
                    offset += 83;
                }
                CompositionFactory.PlayEntrance(TxtPreviewViewBox, offset);

                if (CopySequenceRoot != null && CopySequenceRoot.Visibility == Visibility.Visible)
                    CompositionFactory.PlayEntrance(CopySequenceRoot, offset);
            }
            else if (ViewModel.DisplayMode == FontDisplayMode.TypeRampState)
            {
                CompositionFactory.PlayEntrance(TypeRampInputRow, offset * 2);

                if (TypeRampList != null)
                {
                    List<UIElement> itms = new() { VariableAxis };
                    itms.AddRange(TypeRampList.TryGetChildren());
                    CompositionFactory.PlayEntrance(itms, (offset * 2) + 34);
                }
            }
            else if (ViewModel.DisplayMode == FontDisplayMode.GlyphMapState)
            {
                CompositionFactory.PlayEntrance(GlyphsRoot, offset * 2);
            }
        }
    }

    private void CopySequenceRoot_Loading(FrameworkElement sender, object args)
    {
        CopySequenceRoot.SetTranslation(new Vector3(0, (float)CopySequenceRoot.Height, 0));
        CopySequenceRoot.GetElementVisual().StartAnimation(CompositionFactory.TRANSLATION, CompositionFactory.CreateSlideIn(sender));
    }


    private void AnimateSelectionFromGlyph()
    {
        // Empty glyphs will cause the connected animation service to crash, so manually
        // check if the rendered glyph contains content
        if (ResourceHelper.AllowAnimation
            && GlyphRepeater.ContainerFromItem(GlyphRepeater.SelectedItem) is FrameworkElement container
            && container.GetFirstDescendantOfType<Glyphs>() is Glyphs t)
        {
            t.Measure(container.DesiredSize);
            if (t.DesiredSize.Height != 0 && t.DesiredSize.Width != 0)
            {
                var ani = GlyphRepeater.PrepareConnectedAnimation("PP", GlyphRepeater.SelectedItem, "Text");
                ani.TryStart(TxtPreview);
            }
        }
    }

    void AnimationSelectionFromCharacter()
    {
        // Empty glyphs will cause the connected animation service to crash, so manually
        // check if the rendered glyph contains content
        if (ResourceHelper.AllowAnimation
            && CharGrid.ContainerFromItem(ViewModel.SelectedChar.Char) is FrameworkElement container
            && container.GetFirstDescendantOfType<TextBlock>() is TextBlock t)
        {
            t.Measure(container.DesiredSize);
            if (t.DesiredSize.Height != 0 && t.DesiredSize.Width != 0)
            {
                var ani = CharGrid.PrepareConnectedAnimation("PP", ViewModel.SelectedChar.Char, "Text");
                ani.TryStart(TxtPreview);
                CompositionFactory.PlayEntrance(CharacterInfo.Children.ToList(), 0, 0, 40);
            }
        }
    }



    List<FrameworkElement> GetTypeRampAnimationTargets()
    {
        if (TypeRampRoot is null)
        {
            this.FindName(nameof(TypeRampRoot));

            // Calling measure will force an ItemsControl to populate its
            // ItemsPanel with realized children.
            VariableAxis?.Measure(CharGrid.DesiredSize);
            TypeRampList.Measure(CharGrid.DesiredSize);
        }

        if (TypeRampList.ItemsPanelRoot is null)
            TypeRampList.Measure(MainUIGrid.DesiredSize);

        var items = TypeRampList.ItemsPanelRoot.Children.OfType<FrameworkElement>();

        if (VariableAxis is not null && VariableAxis.ItemsPanelRoot is not null)
            items = items.Concat(VariableAxis.ItemsPanelRoot.Children.OfType<FrameworkElement>());

        return items.Append(TypeRampInputRow).OrderBy(g => Guid.NewGuid()).ToList();
    }

    List<FrameworkElement> GetGridAnimationTargets(ListViewBase control)
    {
        if (control is null)
            return [];

        control.Realize(this.ActualWidth, this.ActualHeight);

        if (control.ItemsPanelRoot is null)
            return [];

        List<FrameworkElement> toChilds = GetChildren(control);
        if (toChilds.Count == 0)
            toChilds = GetChildren(this);

        List<FrameworkElement> GetChildren(FrameworkElement viewport)
        {
            return control.ItemsPanelRoot.Children
                    .OfType<FrameworkElement>()
                    .Where(c => c.IsInViewport(viewport))
                    .Select(c => c is GridViewHeaderItem hi ? (FrameworkElement)hi.ContentTemplateRoot : c)
                    .Concat(control.Header is Panel p ? p.Children.OfType<FrameworkElement>() : new List<FrameworkElement>())
                    .Where(c => c is not null)
                    .OrderBy(c => Guid.NewGuid())
                    .ToList();
        }

        return toChilds;
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

        sb.CreateTimeline(target, Visibility.Collapsed);

        sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(splitter, TargetProperty.CompositeTransform.TranslateX)
            .AddKeyFrame(0.075, 0)
            .AddKeyFrame(0.4, target.RenderSize.Width + splitter.RenderSize.Width, KeySplines.CompositionDefault);

        sb.CreateTimeline(splitter, Visibility.Collapsed);

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

    private Storyboard CreateVerticalHidePane(FrameworkElement target)
    {
        Storyboard sb = new Storyboard();
        Core.Properties.SetTag(sb, target);

        if (target != null && target.Visibility == Visibility.Visible)
        {
            sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(target, TargetProperty.CompositeTransform.TranslateY)
                .AddKeyFrame(CompositionFactory.DefaultOffsetDuration, target.RenderSize.Height, KeySplines.CompositionDefault);

            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(target, TargetProperty.Visibility)
                .AddKeyFrame(0, Visibility.Visible)
                .AddKeyFrame(CompositionFactory.DefaultOffsetDuration, Visibility.Collapsed);
        }

        return sb;
    }

    private Storyboard CreateVerticalShowPane(FrameworkElement target, Storyboard existing = null)
    {
        Storyboard sb = existing ?? new Storyboard();
        Core.Properties.SetTag(sb, target);

        if (target is null)
            return sb;

        if (CopySequenceRoot == target)
        {
            if (existing is not null && sb.Children.OfType<ObjectAnimationUsingKeyFrames>().FirstOrDefault(
                t => Storyboard.GetTargetProperty(t) == TargetProperty.GridRowSpan) 
                    is ObjectAnimationUsingKeyFrames ex)
                ex.AddKeyFrame(0, 3);
            else
                sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(CharGrid, TargetProperty.GridRowSpan)
                    .AddKeyFrame(0, 3);
        }

        if (existing is not null)
        {

        }

        sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(target, TargetProperty.CompositeTransform.TranslateY)
               .AddKeyFrame(0, target.RenderSize.Height)
               .AddKeyFrame(CompositionFactory.DefaultOffsetDuration, 0, KeySplines.CompositionDefault);

        if (existing is not null && sb.Children.OfType<ObjectAnimationUsingKeyFrames>().FirstOrDefault(
               t => Storyboard.GetTargetProperty(t) == TargetProperty.Visibility)
                   is ObjectAnimationUsingKeyFrames ex1)
            ex1.AddKeyFrame(0, Visibility.Visible);
        else
            sb.CreateTimeline(target, Visibility.Visible);

        return sb;
    }

    public void UpdateGridToRampTransition(VisualTransition transition, ListViewBase grid)
    {
        StoryboardBuilderArgs args = new();
        transition.Storyboard = args.Storyboard;

        CreateGridOut(args, grid, true);
        CreateRampIn(args);
    }

    public void UpdateGridToGlyphTransition()
    {
        // 0. Realise items
        this.FindName(nameof(GlyphsRoot));

        if (GlyphRepeater.ItemsPanelRoot is null)
        {
            GlyphRepeater.Measure(CharGrid.DesiredSize);
            if (GlyphRepeater.ItemsPanelRoot is null)
                return;
        }

        StoryboardBuilderArgs args = new();
        GridToGlyphTransition.Storyboard = args.Storyboard;

        CreateGridOut(args, CharGrid, false);
        CreateGridIn(args, GlyphRepeater, false);

        return;
    }


    private void UpdateGlyphLoadedTransition()
    {
        if (GlyphRepeater == null)
            this.FindName(nameof(GlyphsRoot));

        if (GlyphRepeater.ItemsPanelRoot is null)
        {
            GlyphRepeater.Measure(CharGrid.DesiredSize);
            if (GlyphRepeater.ItemsPanelRoot is null)
                return;
        }
        else
        {
            // Force the items to be realised
            GlyphRepeater.Measure(this.DesiredSize);
        }

        StoryboardBuilderArgs args = new();
        GlyphsLoadedTransition.Storyboard = args.Storyboard;
        CreateGridIn(args, GlyphRepeater, false, true);
    }

    public void UpdateRampToGridTransition(ListViewBase grid, VisualTransition t)
    {
        if (TypeRampList == null || grid == null)
            return;

        StoryboardBuilderArgs args = new StoryboardBuilderArgs { FromDepth = 300 };
        t.Storyboard = args.Storyboard;

        CreateRampOut(args);
        CreateGridIn(args, grid, true);
    }

    public void UpdateGlyphToGridTransition()
    {
        if (GlyphRepeater == null)
            return;

        StoryboardBuilderArgs args = new StoryboardBuilderArgs { FromDepth = 300, ToDepth = -400 };
        GlyphToGridTransition.Storyboard = args.Storyboard;
        CreateGridOut(args, GlyphRepeater, false);
        CreateGridIn(args, CharGrid, false);
        return;
    }





    #region PARTS

    void CreateGridOut(StoryboardBuilderArgs args, ListViewBase grid, bool toRamp)
    {
        Storyboard sb = args.Storyboard;

        // 0. Realise items
        if (grid.ItemsPanelRoot is null)
        {
            grid.Measure(grid.DesiredSize);
            if (grid.ItemsPanelRoot is null)
                return;
        }

        // 1.0. Get all the items we'll be animating
        List<FrameworkElement> childs = GetGridAnimationTargets(grid);

        double toDepth = args.ToDepth;

        TimeSpan startOffset = args.CurrentOffset;
        TimeSpan staggerTime = TimeSpan.FromMilliseconds(40);
        TimeSpan duration = TimeSpan.FromMilliseconds(400);
        TimeSpan durationOpacityOut = TimeSpan.FromMilliseconds(150);

        if (toRamp)
        {
            sb.Children.Add(CreateHidePreview(false));

            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(MoreOptionsButton, nameof(MoreOptionsButton.Margin))
                .AddKeyFrame(0, new Thickness(0, 0, -8, 0));

            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(CharGridHeader, TargetProperty.GridColumnSpan)
                .AddKeyFrame(0, 3);
        }
        
        if (grid == CharGrid && !toRamp && CharacterPreviewDetailsRoot.Visibility == Visibility.Visible)
        {
            sb.Children.Add(CreateVerticalHidePane(CharacterPreviewDetailsRoot));
        }
        else// if (!toRamp && CharacterPreviewDetailsRoot.Visibility == Visibility.Visible)
        {
            Storyboard sb1 = new();
            Core.Properties.SetTag(sb1, CharacterPreviewDetailsRoot);
            sb1.CreateTimeline<ObjectAnimationUsingKeyFrames>(CharacterPreviewDetailsRoot, TargetProperty.Visibility)
                .AddKeyFrame(0, Visibility.Collapsed);

            sb.Children.Add(sb1);
        }

        if (toRamp)
        {
            if (grid == CharGrid && GlyphsRoot != null)
                sb.CreateTimeline(GlyphsRoot, Visibility.Collapsed);
            else
                sb.CreateTimeline(CharGrid, Visibility.Collapsed);
        }

        // 3. Animate out Character Grid items
        foreach (var item in childs)
        {
            TimeSpan outStagger = TimeSpan.FromMilliseconds(250d / childs.Count);


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
        sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(grid == GlyphRepeater ? GlyphsRoot : grid, TargetProperty.Visibility)
            .AddKeyFrame(0, Visibility.Visible)
            .AddKeyFrame(startOffset.Add(duration.Multiply(0.8)), Visibility.Collapsed);

        if (grid == CharGrid)
        {
            sb.Children.Add(CreateHideCopyPane(true));

            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(CharacterFilterButton, TargetProperty.Visibility)
                .AddKeyFrame(0, Visibility.Collapsed);

            if (SearchBox.Visibility == Visibility.Visible)
            {
                sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(SearchBox, TargetProperty.Visibility)
                    .AddKeyFrame(0, Visibility.Visible);

                sb.CreateTimeline<DoubleAnimation>(SearchBox, TargetProperty.CompositeTransform.TranslateY)
                    .To(-80)
                    .SetDuration(0.4)
                    .SetEase(new BackEase { Amplitude = 0.8, EasingMode = EasingMode.EaseIn });
            }
        }

        args.CurrentOffset = startOffset;
    }

    void CreateGridIn(StoryboardBuilderArgs args, ListViewBase grid, bool fromRamp, bool gridOnly = false)
    {
        List<FrameworkElement> toChilds = GetGridAnimationTargets(grid);
        
        Storyboard sb = args.Storyboard;
        TimeSpan startOffset = args.CurrentOffset;
        TimeSpan durationOpacityIn = args.DurationOpacityIn;
        TimeSpan charStagger = TimeSpan.FromMilliseconds(250d / (double)(toChilds.Count > 0  ? toChilds.Count : 1));
        double fromDepth = args.FromDepth;

        if (args.CurrentOffset.TotalSeconds > 0)
        {
            sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(grid == GlyphRepeater ? GlyphsRoot : grid, TargetProperty.Visibility)
               .AddKeyFrame(0, Visibility.Collapsed)
               .AddKeyFrame(startOffset, Visibility.Visible);
        }
        else
        {
            //sb.CreateTimeline<ObjectAnimationUsingKeyFrames>(grid == GlyphRepeater ? GlyphsRoot : grid, TargetProperty.Visibility)
            //    .AddKeyFrame(startOffset, Visibility.Visible);
        }
       
        // X. Show PreviewGrid, Splitter, CopyPane
        if (fromRamp && !gridOnly)
            sb.Children.Add(CreateShowPreview(startOffset.TotalSeconds));

        if (grid == CharGrid)
        {
            sb.CreateTimeline(SearchBox, Visibility.Visible);

            sb.CreateTimeline<DoubleAnimationUsingKeyFrames>(SearchBox, TargetProperty.CompositeTransform.TranslateY)
                .AddKeyFrame(0, -80)
                .AddKeyFrame(startOffset, -80)
                .AddKeyFrame(startOffset.TotalSeconds + 0.4, 0, new BackEase { Amplitude = 0.8, EasingMode = EasingMode.EaseOut });

            if (CopySequenceRoot != null)
                sb.Children.Add(CreateVerticalShowPane(CopySequenceContent));

            if (sb.Children.OfType<Storyboard>().FirstOrDefault(s =>
                Core.Properties.GetTag(s) == CharacterPreviewDetailsRoot) is Storyboard existing)
                CreateVerticalShowPane(CharacterPreviewDetailsRoot, existing);
            else
                sb.Children.Add(CreateVerticalShowPane(CharacterPreviewDetailsRoot));
        }
        else
        {
            if (!gridOnly)
            {
                if (sb.Children.OfType<Storyboard>().FirstOrDefault(s =>
                Core.Properties.GetTag(s) == CharacterPreviewDetailsRoot) is not Storyboard existing)
                {
                    sb.CreateTimeline(CharacterPreviewDetailsRoot, Visibility.Collapsed);
                }
            }
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
                    .AddKeyFrame(startOffset.Add(args.Duration), 0, KeySplines.EntranceTheme);
            }

            // 3.4. Increment start offset
            startOffset = startOffset.Add(charStagger);
        }
    }

    void CreateRampIn(StoryboardBuilderArgs args)
    {
        Storyboard sb = args.Storyboard;
        TimeSpan startOffset = args.CurrentOffset;
        TimeSpan staggerTime = TimeSpan.FromMilliseconds(40);
        TimeSpan duration = TimeSpan.FromMilliseconds(400);
        TimeSpan durationOpacityIn = TimeSpan.FromMilliseconds(300);

        // 1.0. Get all the items we'll be animating
        List<FrameworkElement> toChilds = GetTypeRampAnimationTargets();

        // 1.1. Default animation configuration
        double fromDepth = -400;

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

    void CreateRampOut(StoryboardBuilderArgs args)
    {
        List<FrameworkElement> toChilds = GetGridAnimationTargets(CharGrid);
        if (toChilds.Count == 0)
            return;

        Storyboard sb = args.Storyboard;

        var childs = GetTypeRampAnimationTargets();

        double toDepth = -300;

        TimeSpan charStagger = TimeSpan.FromMilliseconds(250d / toChilds.Count);

        TimeSpan startOffset = args.CurrentOffset;
        TimeSpan staggerTime = TimeSpan.FromMilliseconds(40);
        TimeSpan duration = args.Duration;
        TimeSpan durationOpacityOut = TimeSpan.FromMilliseconds(150);

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

        sb.CreateTimeline(TypeRampRoot, Visibility.Collapsed, startOffset.Add(duration.Multiply(0.8)).TotalSeconds);

        args.CurrentOffset = startOffset;
    }

    #endregion
}
