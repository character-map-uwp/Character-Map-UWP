using CharacterMap.Controls;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System.ComponentModel;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Views;

public class TitledDescription
{
    public string Title { get; set; }
    public string Description { get; set; }
}

public class VariantTemplateSelector : DataTemplateSelector
{
    public DataTemplate HeaderTemplate { get; set; }
    public DataTemplate VariantTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
        => item is CMFontFace ? VariantTemplate : HeaderTemplate;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        => item is CMFontFace ? VariantTemplate : HeaderTemplate;
}

[DependencyProperty("TitleLeftContent")]
[DependencyProperty("TitleRightContent")]
[DependencyProperty<FontMapViewModel>("ViewModel")]
[DependencyProperty<bool>("HideTitle")]
[DependencyProperty<FontItem>("Font")]
[AttachedProperty<bool>("GlyphsLoading")]
[AttachedProperty<bool>("GlyphsLoaded")]
[DependencyProperty<GridLength>("BottomHeight")]
public sealed partial class FontMapView : ViewBase, IInAppNotificationPresenter, IPopoverPresenter
{
    private BrushTransition t = new() { Duration = TimeSpan.FromSeconds(0.115) };

    private Debouncer _sizeDebouncer { get; } = new();

    private XamlDirect _xamlDirect { get; }

    private long _previewColumnToken = long.MinValue;

    private bool _isCompactOverlay = false;

    public bool IsStandalone { get; set; }


    public FontMapView()
    {
        InitializeComponent();

        ViewModel = new FontMapViewModel(
            DesignMode ? new DialogService() : Ioc.Default.GetService<IDialogService>());

        if (DesignMode)
            return;

        RequestedTheme = ResourceHelper.GetEffectiveTheme();
        Loading += FontMapView_Loading;

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        CharGrid.ItemSize = ViewModel.Settings.GridSize;
        CharGrid.SetDesiredContainerUpdateDuration(TimeSpan.FromSeconds(1.5));
        _xamlDirect = XamlDirect.GetDefault();
    }

    partial void OnFontChanged(FontItem o, FontItem n)
    {
        if (n is not null)
            ViewModel.SelectedFont = n;
    }

    private void FontMapView_Loading(FrameworkElement sender, object args)
    {
        PaneHideTransition.Storyboard = CreateHidePreview(false, false);
        PaneShowTransition.Storyboard = CreateShowPreview(0, false);

        GoToState(nameof(DefaultGlyphMapState), false);

        if (IsStandalone)
        {
            VisualStateManager.GoToState(this, nameof(StandaloneViewState), false);

            ApplicationView.GetForCurrentView()
                .SetDesiredBoundsMode(ApplicationViewBoundsMode.UseVisible);

            Window.Current.Activate();
            Window.Current.Closed -= Current_Closed;
            Window.Current.Closed += Current_Closed;

            LayoutRoot.KeyDown -= LayoutRoot_KeyDown;
            LayoutRoot.KeyDown += LayoutRoot_KeyDown;
        }
        else
        {
            VisualStateManager.GoToState(this, nameof(ChildViewState), false);
        }
    }

    protected override void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        Register<AppNotificationMessage>(OnNotificationMessage);
        Register<AppSettingsChangedMessage>(OnAppSettingsChanged);
        Register<PrintRequestedMessage>(m =>
        {
            if (Dispatcher.HasThreadAccess)
                TryPrint();
        });
        Register<ExportRequestedMessage>(m =>
        {
            if (Dispatcher.HasThreadAccess)
                TryExport();
        });
        Register<CopyToClipboardMessage>(async m =>
        {
            if (Dispatcher.HasThreadAccess)
                await ViewModel.RequestCopyToClipboardAsync(m);
        });

        UpdateDevUtils(false);
        UpdateDisplayMode();
        UpdateSearchStates();
        UpdateCharacterFit();
        UpdatePaneAndGridSizing();
        UpdateCopyPane();
        UpdateItemsSource();

        PreviewColumn.Width = new GridLength(ViewModel.Settings.LastColumnWidth);
        _previewColumnToken = PreviewColumn.RegisterPropertyChangedCallback(ColumnDefinition.WidthProperty, (d, r) =>
        {
            if (PreviewColumn.Width.Value > 0)
                ViewModel.Settings.LastColumnWidth = PreviewColumn.Width.Value;
        });

        //Visual v = PreviewGrid.EnableTranslation(true).GetElementVisual();
        //PreviewGrid.SetHideAnimation(CompositionFactory.CreateSlideOutX(PreviewGrid));
        //PreviewGrid.SetShowAnimation(CompositionFactory.CreateSlideIn(PreviewGrid));
    }

    protected override void OnUnloaded(object sender, RoutedEventArgs e)
    {
        PreviewColumn.UnregisterPropertyChangedCallback(ColumnDefinition.WidthProperty, _previewColumnToken);
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        LayoutRoot.KeyDown -= LayoutRoot_KeyDown;

        Messenger.UnregisterAll(this);
        ViewModel?.Deactivated();
    }

    public void Cleanup()
    {
        this.Bindings.StopTracking();
    }

    private void Current_Closed(object sender, CoreWindowEventArgs e)
    {
        this.Bindings.StopTracking();
        Window.Current.Closed -= Current_Closed;
        Window.Current.Content = null;
    }

    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ViewModel.SelectedFont):
                UpdateStates();
                UpdateDisplayMode();
                break;
            case nameof(ViewModel.SelectedFace):
                _ = SetCharacterSelectionAsync();
                break;
            case nameof(ViewModel.SelectedTypography):
                UpdateTypography(ViewModel.SelectedTypography);
                break;
            case nameof(ViewModel.SelectedChar):
                if (ViewModel.SelectedChar is not null)
                {
                    try
                    {
                        if (PreviewGrid.Visibility == Visibility.Collapsed || PreviewGridContent.Visibility == Visibility.Collapsed)
                            return;

                        SetWithoutReposition(TxtPreview, _resizerBouncer, () =>
                        {
                            TxtPreview.ClearValue(CharacterMapCX.Controls.DirectText.GlyphIndexProperty);
                            AnimationSelectionFromCharacter();
                        });
                    }
                    catch
                    {
                        // Nu Hair Don't care
                    }

                    //CompositionFactory.PlayScaleEntrance(TxtPreview, .85f, 1f);
                    //CompositionFactory.PlayEntrance(CharacterInfo.Children.ToList(), 0, 0, 40);
                }

                UpdateTypography(ViewModel.SelectedTypography);
                break;
            case nameof(ViewModel.Chars):
                UpdateItemsSource();

                if (ResourceHelper.AllowAnimation)
                    CompositionFactory.PlayEntrance(CharGrid, 166);
                break;
            case nameof(ViewModel.DisplayMode):
                UpdateDisplayMode(true);
                break;
            case nameof(ViewModel.SelectedProvider):
                UpdateDevUtils();
                break;
        }
    }

    private void OnAppSettingsChanged(AppSettingsChangedMessage msg)
    {
        RunOnUI(() =>
        {
            switch (msg.PropertyName)
            {
                case nameof(AppSettings.AllowExpensiveAnimations):
                    CharGrid.EnableResizeAnimation = ViewModel.Settings.AllowExpensiveAnimations;
                    break;
                case nameof(AppSettings.GroupCharacters):
                    UpdateItemsSource();
                    break;
                case nameof(AppSettings.GlyphAnnotation):
                    CharGrid.ItemAnnotation = ViewModel.Settings.GlyphAnnotation;
                    break;
                case nameof(AppSettings.GridSize):
                    UpdateDisplay();
                    break;
                case nameof(AppSettings.UseInstantSearch):
                    UpdateSearchStates();
                    break;
                case nameof(AppSettings.FitCharacter):
                    UpdateCharacterFit();
                    break;
                case nameof(AppSettings.EnablePreviewPane):
                    UpdatePaneAndGridSizing();
                    break;
                case nameof(AppSettings.EnableCopyPane):
                    UpdateCopyPane();
                    break;
                case nameof(AppSettings.UserRequestedTheme):
                    this.RequestedTheme = ResourceHelper.GetEffectiveTheme();
                    break;
            }
        });
    }



    public bool HandleInput(KeyRoutedEventArgs e)
    {
        // If ALT key is held down, ignore
        if (e.KeyStatus.IsMenuKeyDown)
        {
            // Special case for copy as PNG
            if (Utils.IsKeyDown(VirtualKey.Control) && e.Key == VirtualKey.C)
            {
                TryCopy(CopyDataType.PNG);
                return true;
            }
            return false;
        }

        if (Utils.IsKeyDown(VirtualKey.Control))
        {
            switch (e.Key)
            {
                case VirtualKey.C when Utils.IsKeyDown(VirtualKey.Shift):
                    if (ViewModel.SelectedChar.Analysis.IsFullVectorBased)
                        TryCopy(CopyDataType.SVG);
                    break;
                case VirtualKey.C:
                    if (MapDisplayStates.CurrentState == CharacterMapState)
                        TryCopy();
                    break;
                case VirtualKey.G:
                    if (MapDisplayStates.CurrentState == CharacterMapState)
                        ViewModel.Settings.GroupCharacters = !ViewModel.Settings.GroupCharacters;
                    break;
                case VirtualKey.P:
                    FlyoutHelper.PrintRequested();
                    break;
                case VirtualKey.S when ViewModel.SelectedFace is CMFontFace v:
                    ExportManager.RequestExportFontFile(v);
                    break;
                case VirtualKey.E when ViewModel.SelectedFace is CMFontFace:
                    Messenger.Send(new ExportRequestedMessage());
                    break;
                case VirtualKey.Add:
                case (VirtualKey)187:
                    ViewModel.IncreaseCharacterSize();
                    break;
                case VirtualKey.Subtract:
                case (VirtualKey)189:
                    ViewModel.DecreaseCharacterSize();
                    break;
                case VirtualKey.R:
                    if (MapDisplayStates.CurrentState == CharacterMapState)
                        ViewModel.Settings.EnablePreviewPane = !ViewModel.Settings.EnablePreviewPane;
                    break;
                case VirtualKey.B:
                    if (MapDisplayStates.CurrentState == CharacterMapState)
                        ViewModel.Settings.EnableCopyPane = !ViewModel.Settings.EnableCopyPane;
                    break;
                case VirtualKey.T:
                    ViewModel.ChangeDisplayMode();
                    break;
                case VirtualKey.K:
                    _ = QuickCompareView.CreateWindowAsync(new(false));
                    break;
                case VirtualKey.Q when ViewModel.SelectedFace is CMFontFace va:
                    _ = QuickCompareView.AddAsync(ViewModel.RenderingOptions with { Axis = ViewModel.SelectedFaceAnalysis.VariationAxis.Copy() });
                    break;
                case VirtualKey.I:
                    OpenCalligraphy();
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    private void LayoutRoot_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.F11)
            Utils.ToggleFullScreenMode();
        else
            HandleInput(e);
    }

    private void UpdateItemsSource()
    {
        if (ViewModel.Chars == null)
            return;

        var item = CharGrid.SelectedItem;

        if (ViewModel.Settings.GroupCharacters)
        {
            CharGrid.SetBinding(GridView.ItemsSourceProperty, new Binding()
            {
                Source = CharacterSource
            });
            VisualStateManager.GoToState(this, nameof(GroupListState), false);
        }
        else
        {
            CharGrid.ItemsSource = ViewModel.Chars;
            VisualStateManager.GoToState(this, FlatListState.Name, false);
        }

        Dispatcher.Enqueue(() =>
        {
            if (item is not null)
                CharGrid.SelectedItem = item;
        }, CoreDispatcherPriority.Low);
    }

    private void UpdateDevUtils(bool animate = true)
    {
        // We can't bind directly to the setting as it exists 
        // across multiple dispatchers.
        RunOnUI(() =>
        {
            if (ViewModel.SelectedProvider != null)
            {
                if (animate && DevUtilsRoot.Visibility == Visibility.Collapsed && ResourceHelper.AllowAnimation)
                    CompositionFactory.PlayFullHeightSlideUpEntrance(DevUtilsRoot);

                string state = $"Dev{ViewModel.SelectedProvider.Type}State";

                if (!GoToState(state, animate))
                    GoToState(nameof(DevNoneState), animate);
            }
            else
                GoToState(nameof(DevNoneState), animate);
        });
    }

    private void UpdateDisplayMode(bool animate = false)
    {
        // TODO: Could move to a custom VisualStateManager
        if (ViewModel.DisplayMode == FontDisplayMode.TypeRampState)
        {
            if (animate)
            {
                if (MapDisplayStates.CurrentState == CharacterMapState)
                    UpdateGridToRampTransition(GridToRampTransition, CharGrid);
                else if (MapDisplayStates.CurrentState== GlyphMapState)
                    UpdateGridToRampTransition(GlyphToRampTransition, GlyphRepeater);
            }
            GoToState(TypeRampState.Name, animate);
        }
        else if (ViewModel.DisplayMode == FontDisplayMode.CharacterMapState)
        {
            if (animate)
            {
                if (MapDisplayStates.CurrentState == TypeRampState)
                    UpdateRampToGridTransition(CharGrid, RampToGridTransition);
                else if (MapDisplayStates.CurrentState == GlyphMapState)
                    UpdateGlyphToGridTransition();
            }
            else if (CharGrid.ItemsPanelRoot is null)
                CharGrid.Measure(CharGrid.DesiredSize);

            GoToState(CharacterMapState.Name, animate);
        }
        else if (ViewModel.DisplayMode == FontDisplayMode.GlyphMapState)
        {
            if (animate)
            {
                this.FindName(nameof(GlyphsRoot));
                if (MapDisplayStates.CurrentState == CharacterMapState)
                    UpdateGridToGlyphTransition();
                else if (MapDisplayStates.CurrentState == TypeRampState)
                {
                    UpdateRampToGridTransition(GlyphRepeater, RampToGlyphTransition);
                }
            }

            GoToState(GlyphMapState.Name, animate);
        }

        // Make sure this stays in sync with programmatic changes
        ViewSelector.SelectedIndex = (int)ViewModel.DisplayMode;

        //if (animate)
            PlayFontChanged(false);
    }

    private void UpdateCharacterFit()
    {
        if (ViewModel.Settings.FitCharacter)
        {
            TxtPreview.MinHeight = ViewModel.Settings.GridSize;
            TxtPreview.MinWidth = ViewModel.Settings.GridSize;
        }
        else
        {
            TxtPreview.ClearValue(TextBlock.MinWidthProperty);
            TxtPreview.ClearValue(TextBlock.MinHeightProperty);
        }
    }

    private void UpdateSearchStates()
    {
        if (this.IsStandalone)
        {
            GoToState(
              ViewModel.Settings.UseInstantSearch ? nameof(InstantSearchState) : nameof(ManualSearchState));
        }
    }

    private void UpdateStates()
    {
        // Ideally should have been achieved with VisualState setters, buuuuut didn't work for some reason
        GoToState(ViewModel.SelectedFont == null ? NoFontState.Name : HasFontState.Name);
    }

    private void UpdatePaneAndGridSizing()
    {
        // Update VisualState transition
        if (!ViewModel.Settings.EnablePreviewPane)
            PaneHideTransition.Storyboard = CreateHidePreview(false, false);
        else
            PaneShowTransition.Storyboard = CreateShowPreview(0, false);

        GoToState(
              ViewModel.Settings.EnablePreviewPane && !_isCompactOverlay ? nameof(PreviewPaneEnabledState) : nameof(PreviewPaneDisabledState));
    }

    private void UpdateCopyPane()
    {
        string state = ViewModel.Settings.EnableCopyPane && !_isCompactOverlay ? nameof(CopySequenceEnabledState) : nameof(CopySequenceDisabledState);
        if (state == nameof(CopySequenceDisabledState))
            CopyPaneHidingTransition.Storyboard = CreateVerticalHidePane(CopySequenceRoot);
        else
            CopyPaneShowingTransition.Storyboard = CreateVerticalShowPane(CopySequenceRoot);

        GoToState(state);
    }

    private async Task UpdateCompactOverlayAsync()
    {
        var view = ApplicationView.GetForCurrentView();

        if (_isCompactOverlay)
        {
            if (view.ViewMode != ApplicationViewMode.CompactOverlay)
            {
                ViewModePreferences pref = ViewModePreferences.CreateDefault(ApplicationViewMode.CompactOverlay);
                pref.CustomSize = new Windows.Foundation.Size(420, 300);
                pref.ViewSizePreference = ViewSizePreference.Custom;
                if (await view.TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay, pref))
                {
                    GoToState(nameof(CompactOverlayState));
                    SearchBox.PlaceholderText = Localization.Get("SearchBoxShorter");
                    _isCompactOverlay = true;
                }
            }
        }
        else
        {
            if (await view.TryEnterViewModeAsync(ApplicationViewMode.Default))
            {
                GoToState(nameof(NonCompactState));
                SearchBox.PlaceholderText = Localization.Get("SearchBox/PlaceholderText");
                _isCompactOverlay = false;
            }
        }

        UpdatePaneAndGridSizing();
        UpdateCopyPane();
    }

    private string UpdateStatusBarLabel(CMFontFace variant, bool keepCasing)
    {
        if (variant == null)
            return string.Empty;

        string s = Localization.Get("StatusBarCharacterCount", variant.GetCharacters().Count);

        // Hack for Zune Theme.
        if (!keepCasing)
            s = s.ToUpper();

        return s;
    }

    private string UpdateGlyphLabel(CMFontFace variant, bool keepCasing)
    {
        if (variant == null)
            return string.Empty;

        string s = Localization.Get("GlyphsCount", variant.Face.GlyphCount);

        // Hack for Zune Theme.
        if (!keepCasing)
            s = s.ToUpper();

        return s;
    }




    /* Public surface-area methods */

    public void OpenCalligraphy()
    {
        _ = CalligraphyView.CreateWindowAsync(
                ViewModel.RenderingOptions, ViewModel.Sequence);
    }

    public void SelectCharacter(Character ch)
    {
        if (ch is not null)
        {
            CharGrid.SelectedItem = ch;
            CharGrid.ScrollIntoView(ch);
        }
    }

    public void TryCopy(CopyDataType type = CopyDataType.Text)
    {
        if (FocusManager.GetFocusedElement() is TextBox or TextBlock)
            return;

        if (type != CopyDataType.Text)
        {
            ExportStyle style = ExportStyle.Black;
            Character c = ViewModel.SelectedFont?.DisplayMode == FontDisplayMode.CharacterMapState
                ? ViewModel.SelectedChar.Char
                : new GlyphCharacter((ushort)(GlyphRepeater.SelectedItem is uint i ? i : 0));
            if (ViewModel.SelectedChar.GetCharAnalysis(c, ViewModel.SelectedFace).HasColorGlyphs
                && ViewModel.ShowColorGlyphs)
                style = ExportStyle.ColorGlyph;

            _ = ViewModel.RequestCopyToClipboardAsync(
                    new CopyToClipboardMessage(
                        DevValueType.Char,
                        c,
                        ViewModel.SelectedChar.GetCharAnalysis(c, ViewModel.SelectedFace), type)
                    { Style = style });
        }
        else
        {
            TryCopyInternal();
        }
    }

    private async void TryCopyInternal()
    {
        if (CharGrid.SelectedItem is not Character character)
            return;

        Character charToCopy = character;
        bool isVariantCopied = false;

        if (PreviewTypographySelector.SelectedItem 
            is TypographyVariation { IsNone: false, IsVariationMapped: true } variation)
        {
            if (ViewModel.SelectedFace?.TryGetCharacter(variation.FaceCharacterMapping, out Character mappedChar) is true)
                charToCopy = mappedChar;
            else
                charToCopy = new((uint)variation.FaceCharacterMapping);

            isVariantCopied = true;
        }

        if (await Utils.TryCopyToClipboardAsync(charToCopy, ViewModel))
        {
            BorderFadeInStoryboard.Begin();
            bool isVariantSelected = PreviewTypographySelector.SelectedItem is TypographyVariation { IsNone: false };
            TxtCopiedVariantMessage.SetVisible(isVariantSelected && !isVariantCopied);
        }
    }




    //------------------------------------------------------
    //
    //  UI Event Handlers
    //
    //------------------------------------------------------

    private void ToggleCompactOverlay()
    {
        _isCompactOverlay = !_isCompactOverlay;
        _ = UpdateCompactOverlayAsync();
    }

    private void BtnFit_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Settings.FitCharacter = !ViewModel.Settings.FitCharacter;
    }

    private void OnCopyGotFocus(object sender, RoutedEventArgs e)
    {
        ((TextBox)sender).SelectAll();
    }

    private void BtnCopyCode_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext:DevOption o, Tag: string s })
        {
            Utils.CopyToClipBoard(s.Trim());
            BorderFadeInStoryboard.Begin();
            if (!o.SupportsTypography)
                TxtCopiedVariantMessage.SetVisible(PreviewTypographySelector.SelectedItem as TypographyFeatureInfo != TypographyFeatureInfo.None);
            else
                TxtCopiedVariantMessage.SetVisible(false);
        }
    }

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Make sure the PreviewColumn fits properly.
        if (e.NewSize.Width > e.PreviousSize.Width)
            return;

        _sizeDebouncer.Debounce(250, () =>
        {
            if (!this.IsLoaded)
                return;

            // Make sure PreviewColumn stays at a workable size
            if (CharGrid.Visibility == Visibility.Visible)
            {
                int size = (int)CharGrid.ActualWidth + (int)Splitter.ActualWidth + (int)PreviewGrid.ActualWidth;
                if (this.ActualWidth < size && this.ActualWidth < 700)
                {
                    PreviewColumn.Width = new GridLength(Math.Max(0, (int)(this.ActualWidth - CharGrid.ActualWidth - Splitter.ActualWidth)));
                }
            }
        });
    }

    private void OnSearchBoxGotFocus(AutoSuggestBox searchBox)
    {
        if (ViewModel.Search.Results is not null)
            searchBox.IsSuggestionListOpen = true;
        else
            ViewModel.Search.DebounceSearch(ViewModel.Search.Query, ViewModel.Settings.InstantSearchDelay);
    }

    internal void OnSearchBoxSubmittedQuery(AutoSuggestBox searchBox)
    {
        // commented below line because it will keep search result list open even when user selected an item in search result
        // searchBox.IsSuggestionListOpen = true;
        if (!string.IsNullOrWhiteSpace(ViewModel.Search.Query))
        {
            ViewModel.Search.DebounceSearch(ViewModel.Search.Query, ViewModel.Settings.InstantSearchDelay, SearchSource.ManualSubmit);
        }
        else
        {
            ViewModel.Search.Results = null;
        }
    }

    internal void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput
            && args.CheckCurrent()
            && string.IsNullOrEmpty(sender.Text)
            && !ViewModel.Settings.UseInstantSearch)
        {
            ViewModel.Search.DebounceSearch(sender.Text, 0, SearchSource.ManualSubmit);
        }
    }

    internal void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is IGlyphData data)
        {
            if (ViewModel.Chars.FirstOrDefault(c => c.UnicodeIndex == data.UnicodeIndex) is Character c)
                SelectCharacter(c);
            else
                ViewModel.Notify(Localization.Get("NotificationSearchSelectedCharHidden"));
        }
    }

    internal void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        OnSearchBoxGotFocus(sender as AutoSuggestBox);
    }

    public void SearchBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel.Settings.UseInstantSearch && e.Key == VirtualKey.Enter)
            e.Handled = true;
    }

    internal void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        OnSearchBoxSubmittedQuery(sender);
    }

    private void SearchBox_ShortcutInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (SearchBox.FocusState == FocusState.Unfocused)
            SearchBox.Focus(FocusState.Keyboard);
    }

    private void InfoFlyout_Opened(object sender, object e)
    {
        if (sender is Flyout f)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (f.Content.GetFirstAncestorOfType<ScrollViewer>() is ScrollViewer sv)
                sv.ScrollToVerticalOffset(0);
#pragma warning restore CS0618
        }
    }

    private void MenuFlyout_Opening(object sender, object e)
    {
        if (ViewModel.RenderingOptions is null)
        {
            // Startup has gone wrong, ViewModel has no selected character
            ViewModel.SetDefaultChar();
            MoreMenu.Hide();
        }

        if (ViewModel.SelectedFont is FontItem item)
        {
            FlyoutHelper.CreateMenu(
                MoreMenu,
                item.Font,
                ViewModel.RenderingOptions with { Axis = ViewModel.SelectedFaceAnalysis.VariationAxis.Copy(), Family = ViewModel.SelectedFont.Font },
                this.Tag as FrameworkElement,
                new()
                {
                    Standalone = IsStandalone,
                    ShowAdvanced = true,
                    IsExternalFile = ViewModel.IsExternalFile,
                    Folder = ViewModel.Folder,
                    PreviewText = ViewModel.Sequence
                });
        }
    }

    private void DevFlyout_Opening(object sender, object e)
    {
        if (sender is MenuFlyout { Items: { Count: > 2 } } menu 
            && ViewModel.Providers is not null)
        {
            Style style = ResourceHelper.Get<Style>("ThemeMenuFlyoutItemStyle");
            foreach (var provider in ViewModel.Providers)
            {
                MenuFlyoutItem item = new()
                {
                    Command = ViewModel.SetDevCommand,
                    CommandParameter = provider.Type,
                    Text = provider.DisplayName,
                    Style = style
                };
                menu.Items.Add(item);
            }
        }
    }

    private void EditSuggestions_Click(object sender, RoutedEventArgs e)
    {
        Messenger.Send(new EditSuggestionsRequested());
    }

    private Visibility ShowFilePath(string filePath, bool isImported)
    {
        if (!isImported && !string.IsNullOrWhiteSpace(filePath))
            return Visibility.Visible;

        return Visibility.Collapsed;
    }

    private Task SetCharacterSelectionAsync()
    {
        return Dispatcher.ExecuteAsync(() =>
        {
            if (null != CharGrid.SelectedItem)
            {
                CharGrid.ScrollIntoView(CharGrid.SelectedItem, ScrollIntoViewAlignment.Default);
            }
        }, CoreDispatcherPriority.Low);
    }

    private void Slider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        ViewModel.UpdateVariations();
    }

    private void AxisReset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement {Tag: Slider s, DataContext: DWriteFontAxis axis })
            s.Value = axis.DefaultValue;
    }

    private void CharGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.ItemContainer is not null)
        {
            args.ItemContainer.ContextRequested -= Grid_ContextRequested;
            args.ItemContainer.ContextRequested += Grid_ContextRequested;

            var c = VisualTreeHelper.GetChild(args.ItemContainer, 0);
            var b = VisualTreeHelper.GetChild(c, 0);
            if (b is Border br)
                br.BackgroundTransition = ResourceHelper.AllowAnimation ? t : null;
        }
    }

    private void Grid_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (sender is FrameworkElement f
            && f.GetDescendantsOfType<Grid>().FirstOrDefault(g => g.Tag is Character || g.Name == "Target") is Grid grid)
        {
            /* Context menu for character grid or glyph grid */
            args.Handled = true;
            if (grid.Tag is Character c)
            {
                FlyoutHelper.ShowCharacterGridContext(GridContextFlyout, grid, ViewModel, IsStandalone);
            }
            else if (grid.Tag is int glyphIndex)
            {
                FlyoutHelper.ShowCharacterGridContext(GridContextFlyout, grid, ViewModel, IsStandalone, new GlyphCharacter((ushort)glyphIndex));
            }
        }
    }

    private void SavePng_Click(object sender, RoutedEventArgs e)
    {
        /* Save from Character Grid Context Menu */
        if (sender is MenuFlyoutItem { DataContext: Character c, CommandParameter: ExportStyle style } item)
        {
            if (item.Tag is null)
            {
                _ = ViewModel.SavePngAsync(new()
                {
                    Style = style,
                    Typography = ViewModel.SelectedTypography.Feature,
                    Character= c
                });
            }
            else
            {
                _ = ViewModel.RequestCopyToClipboardAsync(
                    new CopyToClipboardMessage(
                        DevValueType.Char, c, ViewModel.SelectedChar.GetCharAnalysis(c, ViewModel.SelectedFace), CopyDataType.PNG)
                    { Style = style });
            }
        }
    }

    private void SaveSvg_Click(object sender, RoutedEventArgs e)
    {
        /* Save from Character Grid Context Menu */
        if (sender is MenuFlyoutItem { DataContext: Character c, CommandParameter: ExportStyle style } item)
        {
            if (item.Tag is null)
            {
                _ = ViewModel.SaveSvgAsync(new()
                {
                    Style = style,
                    Typography = ViewModel.SelectedTypography.Feature,
                     Character = c
                });
            }
            else
            {
                _ = ViewModel.RequestCopyToClipboardAsync(
                    new CopyToClipboardMessage(
                        DevValueType.Char, c, ViewModel.SelectedChar.GetCharAnalysis(c, ViewModel.SelectedFace), CopyDataType.SVG)
                    { Style = style });
            }
        }
    }

    private void CopyClick(object sender, RoutedEventArgs e)
    {
        /* Copy from Character Grid Context Menu */
        if (sender is MenuFlyoutItem { DataContext: Character c, CommandParameter: DevValueType type })
        {
            _ = ViewModel.RequestCopyToClipboardAsync(
                    new CopyToClipboardMessage(type, c, ViewModel.SelectedChar.GetCharAnalysis(c, ViewModel.SelectedFace)));
        }
    }

    private void AddClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { DataContext: Character c })
            ViewModel.Sequence += c.Char;
    }

    private void OpenCalligraphyClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem {DataContext: Character c })
        {
            _ = CalligraphyView.CreateWindowAsync(
                    ViewModel.RenderingOptions, c.Char);
        }
    }

    private void PaneButton_Loaded(object sender, RoutedEventArgs e)
    {
        ((AppBarToggleButton)sender).IsChecked = ResourceHelper.AppSettings.EnablePreviewPane;
    }

    private void ToggleCopyPaneButton_Loaded(object sender, RoutedEventArgs e)
    {
        ((AppBarToggleButton)sender).IsChecked = ResourceHelper.AppSettings.EnableCopyPane;
    }

    private void CategoryFlyout_AcceptClicked(object sender, IList<UnicodeRangeModel> e)
    {
        ViewModel.UpdateCategories(e);
    }

    private void CharGrid_ItemDoubleTapped(object sender, Character e)
    {
        AddCharToSequence(e);
    }

    private void AppBarButton_Click(object sender, RoutedEventArgs e)
    {
        AddCharToSequence(ViewModel.SelectedChar.Char);
    }

    async void AddCharToSequence(Character c)
    {
        if (CopySequenceText == null)
            return;

        int selection = CopySequenceText.SelectionStart;
        ViewModel.AddCharToSequence(
            CopySequenceText.SelectionStart,
            CopySequenceText.SelectionLength,
            ViewModel.SelectedChar);
        CopySequenceText.SelectionStart = selection + 1;

        await Task.Delay(64);
        CopySequenceText.Focus(FocusState.Programmatic);
    }

    private void PreviewTypographySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PreviewTypographySelector.SelectedItem is TypographyVariation tv)
            UpdateTypography(tv, true);
        else if (PreviewTypographySelector.SelectedItem is TypographyFeatureInfo info)
        {
            Debugger.Break();
        }
    }

    private void InfoFlyout_Opening(object sender, object e)
    {
#pragma warning disable CS0618 // ChangeView doesn't actually work
        // Reset flyout scroll position
        InfoScroller.ScrollToVerticalOffset(0);
#pragma warning restore CS0618
    }

    private void TypeRampFlowButton_Click(object sender, RoutedEventArgs e)
    {
        TypeRampScroller.FlowDirection = TypeRampScroller.FlowDirection == FlowDirection.LeftToRight
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        _ = new InstallFontDialog(ViewModel.SelectedFaceAnalysis.Analysis).ShowAsync(ContentDialogPlacement.Popup);
    }

    private void AdvancedSettings_Click(object sender, RoutedEventArgs e)
    {
        Messenger.Send<AdvancedOptionsRequested>();
    }

    private void VariableHintClick(object sender, RoutedEventArgs e)
    {
        ViewSelector.TryFocusOn(2, FocusState.Keyboard);
    }

    private void FilterHint_Click(object sender, RoutedEventArgs e)
    {
        CharacterFilterButton.Focus(FocusState.Keyboard);
    }

    private void FindClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { DataContext: Character c }
            && this.IsStandalone == false
            && this.GetFirstAncestorOfType<MainPage>() is { } m)
        {
            SelectCharacter(c);
            m.CharacterSearch(c.Char);
        }
    }

    private void BtnGlyph_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GlyphCharacter g })
            NavigateToGlyph(g.GlyphIndex);
    }

    public async void NavigateToGlyph(ushort glyphIndex)
    {
        ViewModel.DisplayMode = FontDisplayMode.GlyphMapState;

        if (ViewModel.SelectedFaceAnalysis?.Glyphs is GlyphCollection coll)
            await coll.EnsureLoadedUpToAsync(glyphIndex);

        this.Enqueue(() =>
        {
            GlyphRepeater.SelectedItem = (uint)glyphIndex;
            GlyphRepeater.ScrollIntoView((uint)glyphIndex);
        }, CoreDispatcherPriority.Low);
    }

    private void ToolTip_Opened(object sender, RoutedEventArgs e)
    {
        _ = ViewModel?.SelectedFaceAnalysis?.LoadGlyphFontAsync();
    }




    //------------------------------------------------------
    //
    //  Printing
    //
    //------------------------------------------------------

    FontMapView IPopoverPresenter.GetFontMap() => this;

    Border IPopoverPresenter.GetPresenter()
    {
        this.FindName(nameof(PrintPresenter));
        return PrintPresenter;
    }

    public GridLength GetTitleBarHeight() => TitleBar.TemplateSettings.GridHeight;

    private void TryPrint()
    {
        if (this.GetFirstAncestorOfType<MainPage>() is null)
            PrintView.Show(this);
    }

    private void TryExport()
    {
        if (this.GetFirstAncestorOfType<MainPage>() is null)
            ExportView.Show(this);
    }





    //------------------------------------------------------
    //
    //  Notifications
    //
    //------------------------------------------------------

    public InAppNotification GetNotifier()
    {
        if (NotificationRoot == null)
            this.FindName(nameof(NotificationRoot));

        return DefaultNotification;
    }

    void OnNotificationMessage(AppNotificationMessage msg)
    {
        if (Dispatcher.HasThreadAccess && !this.IsStandalone)
            return;

        InAppNotificationHelper.OnMessage(this, msg);
    }




    /* Character Grid Binding Helpers */

    Character GetChar(CharacterAnalysisModel c) => c?.Char;

    void ToModel(object c) => ViewModel.SelectedChar = new (ViewModel.SelectedFace, c as Character, ViewModel);

    private void UpdateDisplay()
    {
        TxtPreview.FontSize = Core.Converters.GetFontSize(ViewModel.Settings.GridSize);
        if (ViewModel.Settings.FitCharacter)
        {
            TxtPreview.MinHeight = ViewModel.Settings.GridSize;
            TxtPreview.MinWidth = ViewModel.Settings.GridSize;
        }

        RunOnUI(async () =>
        {
            if (!this.IsLoaded)
                return;

            if (ViewModel.Settings.AllowExpensiveAnimations)
            {
                CharGrid.UpdateSize(ViewModel.Settings.GridSize);
            }
            else
            {
                // We apply the size changes by clearing the ItemsSource and resetting it,
                // allowing the GridView to re-layout all of it's items with their new size.
                CharGrid.ItemsSource = null;
                CharGrid.ItemSize = ViewModel.Settings.GridSize;
                await Task.Yield();
                CharGrid.SetBinding(GridView.ItemsSourceProperty, new Binding() { Source = CharacterSource });
                ViewModel.SetDefaultChar();
                _ = SetCharacterSelectionAsync();
            }
        });
    }

    void UpdateTypography(TypographyVariation info, bool previewOnly = false)
    {
        if (ViewModel.IsLoadingCharacters || ViewModel.Chars == null)
            return;

        if (CharGrid.ItemsSource != null && CharGrid.ItemsPanelRoot != null)
        {
            ViewModel.SelectedCharTypography = info.Feature;
            IXamlDirectObject p = _xamlDirect.GetXamlDirectObject(TxtPreview);
            CharacterGridView.UpdateTypography(_xamlDirect, p, info.Feature);
        }

        if (CopySequenceText != null && !previewOnly)
        {
            IXamlDirectObject p = _xamlDirect.GetXamlDirectObject(CopySequenceText);
            CharacterGridView.UpdateTypography(_xamlDirect, p, info.Feature);
        }
    }

    Visibility GetAlternatesVis(TypographyVariation global, List<TypographyVariation> info)
    {
        Visibility vis = info == null || info.Count <= 1 ? Visibility.Collapsed : Visibility.Visible;

        void Update()
        {
            if (ViewModel.SelectedChar?.Variations is { } vars)
            {
                // The character might not support the current ViewModel typography, so make sure we fallback
                // too an appropriate selection
                if (vars.FirstOrDefault(v => v.Feature == global.Feature) is { } match)
                    PreviewTypographySelector.SelectedItem = match;
                else
                    PreviewTypographySelector.SelectedItem = vars.FirstOrDefault();
            }
        }

        Update();

        // Hack to ensure properly set when switching between characters in a font
        this.Enqueue(Update);

        return vis;
    }






    //------------------------------------------------------
    //
    //  Glyph Map
    //
    //------------------------------------------------------

    static partial void OnGlyphsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FontMapView view && e.NewValue is bool b && b)
            view.GoToState(nameof(view.GlyphMapLoadingState));
    }

    static partial void OnGlyphsLoadedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FontMapView view && e.NewValue is bool b && b)
        {
            view.Enqueue(() =>
            {
                if (view.GlyphMapStates.CurrentState == view.GlyphMapLoadingState)
                    view.UpdateGlyphLoadedTransition();

                view.GoToState(nameof(view.GlyphMapLoadedState));
            });
        }
    }

    private int ToInt(FontDisplayMode mode) => (int)mode;

    Debouncer _resizerBouncer = new(250);

    private void GlyphRepeater_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GlyphRepeater.SelectedItem is uint i)
        {
            SetWithoutReposition(TxtPreview, _resizerBouncer, () =>
            {
                TxtPreview.GlyphIndex = (int)i;
                AnimateSelectionFromGlyph();
            });
        }
    }

    static void SetWithoutReposition(FrameworkElement repositionTarget, Debouncer debouncer, Action action)
    {
        CompositionFactory.SetUseWindowAwareSynchronisedReposition(repositionTarget, false);

        action?.Invoke();

        debouncer.Debounce(
            () => CompositionFactory.SetUseWindowAwareSynchronisedReposition(repositionTarget, true));
    }
}





//------------------------------------------------------
//
//  Window Handling
//
//------------------------------------------------------

public partial class FontMapView
{
    public static async Task CreateNewViewForFontAsync(CMFontFamily font, StorageFile sourceFile = null, CharacterRenderingOptions options = null)
    {
        void CreateView()
        {
            FontItem item = new FontItem(font);
            item.Selected = options?.Variant ?? font.DefaultVariant;

            FontMapView map = new()
            {
                IsStandalone = true,
                ViewModel = { SelectedFont = item, IsExternalFile = sourceFile != null, SourceFile = sourceFile }
            };

            // Attempt to apply any custom rendering options from the source view
            if (options != null && options.Variant != null && font.Variants.Contains(options.Variant))
            {
                if (options.DefaultTypography != null)
                    map.ViewModel.SelectedTypography = new(options.DefaultTypography);
            }

            Window.Current.Content = map;
            Window.Current.Activate();
        }

        var view = await WindowService.CreateViewAsync(CreateView, false);
        await WindowService.TrySwitchToWindowAsync(view, false);
    }
}
