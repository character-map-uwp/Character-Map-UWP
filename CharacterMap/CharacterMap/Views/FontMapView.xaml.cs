using CharacterMap.Controls;
using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using CharacterMapCX;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Views
{
    public sealed partial class FontMapView : ViewBase, IInAppNotificationPresenter, IPopoverPresenter
    {
        #region Dependency Properties 

        #region TitleLeftContent

        public object TitleLeftContent
        {
            get { return (object)GetValue(TitleLeftContentProperty); }
            set { SetValue(TitleLeftContentProperty, value); }
        }

        // Using a DependencyProperty as the backing store for TitleLeftContent.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TitleLeftContentProperty =
            DependencyProperty.Register(nameof(TitleLeftContent), typeof(object), typeof(FontMapView), new PropertyMetadata(null));

        #endregion

        #region TitleRightContent

        public object TitleRightContent
        {
            get { return (object)GetValue(TitleRightContentProperty); }
            set { SetValue(TitleRightContentProperty, value); }
        }

        // Using a DependencyProperty as the backing store for TitleRightContent.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TitleRightContentProperty =
            DependencyProperty.Register(nameof(TitleRightContent), typeof(object), typeof(FontMapView), new PropertyMetadata(null));

        #endregion

        #region Font

        public FontItem Font
        {
            get => (FontItem)GetValue(FontProperty);
            set => SetValue(FontProperty, value);
        }

        public static readonly DependencyProperty FontProperty =
            DependencyProperty.Register(nameof(Font), typeof(FontItem), typeof(FontMapView), new PropertyMetadata(null, (d, e) =>
            {
                if (d is FontMapView f && e.NewValue is FontItem item)
                    f.ViewModel.SelectedFont = item;
            }));

        #endregion

        #region ViewModel

        public FontMapViewModel ViewModel
        {
            get => (FontMapViewModel)GetValue(ViewModelProperty);
            private set => SetValue(ViewModelProperty, value);
        }

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(FontMapViewModel), typeof(FontMapView), new PropertyMetadata(null));

        #endregion

        #region Hide Title

        public bool HideTitle
        {
            get { return (bool)GetValue(HideTitleProperty); }
            set { SetValue(HideTitleProperty, value); }
        }

        public static readonly DependencyProperty HideTitleProperty =
            DependencyProperty.Register(nameof(HideTitle), typeof(bool), typeof(FontMapView), new PropertyMetadata(false));

        #endregion

        #endregion

        public bool IsStandalone { get; set; }

        private Debouncer _sizeDebouncer { get; } = new Debouncer();

        private XamlDirect _xamlDirect { get; }

        private long _previewColumnToken = long.MinValue;

        private bool _isCompactOverlay = false;

        public FontMapView()
        {
            InitializeComponent();

            ViewModel = new FontMapViewModel(
                DesignMode ? new DialogService() : Ioc.Default.GetService<IDialogService>(), 
                ResourceHelper.AppSettings);

            if (DesignMode)
                return;

            RequestedTheme = ResourceHelper.GetEffectiveTheme();
            Loading += FontMapView_Loading;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            CharGrid.ItemSize = ViewModel.Settings.GridSize;
            CharGrid.SetDesiredContainerUpdateDuration(TimeSpan.FromSeconds(1.5));
            _xamlDirect = XamlDirect.GetDefault();
        }

        private void FontMapView_Loading(FrameworkElement sender, object args)
        {
            PaneHideTransition.Storyboard = CreateHidePreview(false, false);
            PaneShowTransition.Storyboard = CreateShowPreview(0, false);

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
                    UpdateDisplayMode(false);
                    break;
                case nameof(ViewModel.SelectedVariant):
                    _ = SetCharacterSelectionAsync();
                    break;
                case nameof(ViewModel.SelectedTypography):
                    UpdateTypography(ViewModel.SelectedTypography);
                    break;
                case nameof(ViewModel.SelectedChar):
                    if (ResourceHelper.AllowAnimation)
                    {
                        if (ViewModel.SelectedChar is not null)
                        {
                            try
                            {
                                if (PreviewGrid.Visibility == Visibility.Collapsed || PreviewGridContent.Visibility == Visibility.Collapsed)
                                    return;

                                // Empty glyphs will cause the connected animation service to crash, so manually
                                // check if the rendered glyph contains content
                                if (CharGrid.ContainerFromItem(ViewModel.SelectedChar) is FrameworkElement container
                                    && container.GetFirstDescendantOfType<TextBlock>() is TextBlock t)
                                {
                                    t.Measure(container.DesiredSize);
                                    if (t.DesiredSize.Height != 0 && t.DesiredSize.Width != 0)
                                    {
                                        var ani = CharGrid.PrepareConnectedAnimation("PP", ViewModel.SelectedChar, "Text");
                                        ani.TryStart(TxtPreview);
                                        CompositionFactory.PlayEntrance(CharacterInfo.Children.ToList(), 0, 0, 40);
                                    }
                                }
                            }
                            catch
                            {
                                // Nu Hair Dont care
                            }
                        }

                        //CompositionFactory.PlayScaleEntrance(TxtPreview, .85f, 1f);
                        //CompositionFactory.PlayEntrance(CharacterInfo.Children.ToList(), 0, 0, 40);
                    }

                    UpdateTypography(ViewModel.SelectedTypography);
                    break;
                case nameof(ViewModel.Chars):
                    CharGrid.ItemsSource = ViewModel.Chars;
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
                return false;

            var ctrlState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Control);
            if ((ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
            {
                switch (e.Key)
                {
                    case VirtualKey.C:
                        TryCopy();
                        break;
                    case VirtualKey.P:
                        FlyoutHelper.PrintRequested();
                        break;
                    case VirtualKey.S when ViewModel.SelectedVariant is FontVariant v:
                            ExportManager.RequestExportFontFile(v);
                        break;
                    case VirtualKey.E when ViewModel.SelectedVariant is FontVariant:
                            WeakReferenceMessenger.Default.Send(new ExportRequestedMessage());
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
                        ViewModel.Settings.EnablePreviewPane = !ViewModel.Settings.EnablePreviewPane;
                        break;
                    case VirtualKey.B:
                        ViewModel.Settings.EnableCopyPane = !ViewModel.Settings.EnableCopyPane;
                        break;
                    case VirtualKey.T:
                        ViewModel.ChangeDisplayMode();
                        break;
                    case VirtualKey.K:
                        _ = QuickCompareView.CreateWindowAsync(new(false));
                        break;
                    case VirtualKey.Q when ViewModel.SelectedVariant is FontVariant va:
                            _ = QuickCompareView.AddAsync(ViewModel.RenderingOptions with { Axis = ViewModel.VariationAxis.Copy() });
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
            if (ViewModel.DisplayMode == FontDisplayMode.TypeRamp)
            {
                if (animate)
                    UpdateGridToRampTransition();
                GoToState(TypeRampState.Name, animate);
            }
            else
            {
                if (animate)
                    UpdateRampToGridTransition();
                else if (CharGrid.ItemsPanelRoot is null)
                    CharGrid.Measure(CharGrid.DesiredSize);

                GoToState(CharacterMapState.Name, animate);
            }

            if (animate)
            {
                PlayFontChanged(false);
            }
        }

        private void UpdateCharacterFit()
        {
            if (ViewModel.Settings.FitCharacter)
            {
                ZoomOutGlyph.Visibility = Visibility.Visible;
                ZoomGlyph.Visibility = Visibility.Collapsed;

                TxtPreview.MinHeight = ViewModel.Settings.GridSize;
                TxtPreview.MinWidth = ViewModel.Settings.GridSize;
            }
            else
            {
                TxtPreview.ClearValue(TextBlock.MinWidthProperty);
                TxtPreview.ClearValue(TextBlock.MinHeightProperty);

                ZoomOutGlyph.Visibility = Visibility.Collapsed;
                ZoomGlyph.Visibility = Visibility.Visible;
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

            // OverlayButton might not be inflated so can't use VisualState
            //OverlayButton?.SetVisible(IsStandalone);
        }

        private void UpdateCopyPane()
        {
            string state = ViewModel.Settings.EnableCopyPane && !_isCompactOverlay ? nameof(CopySequenceEnabledState) : nameof(CopySequenceDisabledState);
            if (state == nameof(CopySequenceDisabledState))
                CopyPaneHidingTransition.Storyboard = CreateHideCopyPane();
            else
                CopyPaneShowingTransition.Storyboard = CreateShowCopyPane();

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

        private string UpdateStatusBarLabel(FontVariant variant, bool keepCasing)
        {
            if (variant == null)
                return string.Empty;

            string s = Localization.Get("StatusBarCharacterCount", variant.Characters.Count);

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
            if (null != ch)
            {
                CharGrid.SelectedItem = ch;
                CharGrid.ScrollIntoView(ch);
            }
        }

        public void TryCopy()
        {
            //if (CharGrid.SelectedItem is Character character &&
            //    (TxtSymbolIcon == null || !TxtSymbolIcon.SelectedText.Any()) &&
            //    !TxtFontIcon.SelectedText.Any() &&
            //    !TxtXamlCode.SelectedText.Any())
            {
                TryCopyInternal();
            }
        }

        private async void TryCopyInternal()
        {
            if (CharGrid.SelectedItem is Character character
                && await Utils.TryCopyToClipboardAsync(character, ViewModel))
            {
                BorderFadeInStoryboard.Begin();
                TxtCopiedVariantMessage.SetVisible(PreviewTypographySelector.SelectedItem as TypographyFeatureInfo != TypographyFeatureInfo.None);
            }
        }




        /* UI Event Handlers */

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
            if (sender is FrameworkElement f 
                && f.DataContext is DevOption o
                && f.Tag is string s)
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
            if (ViewModel.SearchResults != null && ViewModel.SearchResults.Count > 0)
                searchBox.IsSuggestionListOpen = true;
            else
                ViewModel.DebounceSearch(ViewModel.SearchQuery, ViewModel.Settings.InstantSearchDelay);
        }

        internal void OnSearchBoxSubmittedQuery(AutoSuggestBox searchBox)
        {
            // commented below line because it will keep search result list open even when user selected an item in search result
            // searchBox.IsSuggestionListOpen = true;
            if (!string.IsNullOrWhiteSpace(ViewModel.SearchQuery))
            {
                ViewModel.DebounceSearch(ViewModel.SearchQuery, ViewModel.Settings.InstantSearchDelay, SearchSource.ManualSubmit);
            }
            else
            {
                ViewModel.SearchResults = null;
            }
        }

        internal void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput
                && args.CheckCurrent()
                && string.IsNullOrEmpty(sender.Text)
                && !ViewModel.Settings.UseInstantSearch)
            {
                ViewModel.DebounceSearch(sender.Text, 0, SearchSource.ManualSubmit);
            }
        }

        internal void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is IGlyphData data)
            {
                SelectCharacter(ViewModel.Chars.First(c => c.UnicodeIndex == data.UnicodeIndex));
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
            if (ViewModel.SelectedFont is FontItem item)
            {
                FlyoutHelper.CreateMenu(
                    MoreMenu,
                    item.Font,
                    ViewModel.RenderingOptions with { Axis = ViewModel.VariationAxis.Copy() },
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
            if (sender is MenuFlyout menu && menu.Items.Count < 2)
            {
                Style style = ResourceHelper.Get<Style>("ThemeMenuFlyoutItemStyle");
                foreach (var provider in ViewModel.Providers)
                {
                    MenuFlyoutItem item = new()
                    {
                        Command = ViewModel.ToggleDev,
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
            return Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                if (null != CharGrid.SelectedItem)
                {
                    CharGrid.ScrollIntoView(ViewModel.SelectedChar, ScrollIntoViewAlignment.Default);
                }
            }).AsTask();
        }

        private void Slider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            ViewModel.UpdateVariations();
        }

        private void AxisReset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement b
                && b.Tag is Slider s
                && b.DataContext is DWriteFontAxis axis)
            {
                s.Value = axis.DefaultValue;
            }
        }

        private void CharGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (!args.InRecycleQueue && args.ItemContainer is not null)
            {
                args.ItemContainer.ContextRequested -= Grid_ContextRequested;
                args.ItemContainer.ContextRequested += Grid_ContextRequested;
            }
        }

        private void Grid_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (sender is FrameworkElement f 
                && f.GetDescendantsOfType<Grid>().FirstOrDefault(g => g.Tag is Character) is Grid grid)
            {
                /* Context menu for character grid */
                args.Handled = true;
                FlyoutHelper.ShowCharacterGridContext(GridContextFlyout, grid, ViewModel);
            }
        }

        private void SavePng_Click(object sender, RoutedEventArgs e)
        {
            /* Save from Character Grid Context Menu */
            if (sender is MenuFlyoutItem item
                && item.DataContext is Character c
                && item.CommandParameter is ExportStyle style)
            {
                _ = ViewModel.SavePngAsync(new ExportParameters
                {
                    Style = style,
                    Typography = ViewModel.SelectedTypography
                }, c);
            }
        }

        private void SaveSvg_Click(object sender, RoutedEventArgs e)
        {
            /* Save from Character Grid Context Menu */
            if (sender is MenuFlyoutItem item
                && item.DataContext is Character c
                && item.CommandParameter is ExportStyle style)
            {
                _ = ViewModel.SaveSvgAsync(new ExportParameters
                {
                    Style = style,
                    Typography = ViewModel.SelectedTypography
                }, c);
            }
        }

        private void CopyClick(object sender, RoutedEventArgs e)
        {
            /* Copy from Character Grid Context Menu */
            if (sender is MenuFlyoutItem item
              && item.DataContext is Character c
              && item.CommandParameter is DevValueType type)
            {
                _ = ViewModel.RequestCopyToClipboardAsync(
                        new CopyToClipboardMessage(type, c, ViewModel.GetCharAnalysis(c)));
            }
        }

        private void AddClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item
                && item.DataContext is Character c)
            {
                ViewModel.Sequence += c.Char;
            }
        }

        private void OpenCalligraphyClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item
                && item.DataContext is Character c)
            {
                _ = CalligraphyView.CreateWindowAsync(
                        ViewModel.RenderingOptions, c.Char);
            }
        }

        private void PaneButton_Loaded(object sender, RoutedEventArgs e)
        {
            ((AppBarToggleButton)sender).IsChecked = !ResourceHelper.AppSettings.EnablePreviewPane;
        }

        private void ToggleCopyPaneButton_Loaded(object sender, RoutedEventArgs e)
        {
            ((AppBarToggleButton)sender).IsChecked = ResourceHelper.AppSettings.EnableCopyPane;
        }

        private void CategoryFlyout_AcceptClicked(object sender, IList<UnicodeCategoryModel> e)
        {
            ViewModel.UpdateCategories(e);
        }

        private void CharGrid_ItemDoubleTapped(object sender, Character e)
        {
            AddCharToSequence(e);
        }

        private void AppBarButton_Click(object sender, RoutedEventArgs e)
        {
            AddCharToSequence(ViewModel.SelectedChar);
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
            UpdateTypography(PreviewTypographySelector.SelectedItem as TypographyFeatureInfo, true);
        }




        /* Print Helpers */

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




        /* Notification Helpers */

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
                    CharGrid.ItemsSource = ViewModel.Chars;
                    ViewModel.SetDefaultChar();
                    _ = SetCharacterSelectionAsync();
                }
            });
        }

        void UpdateTypography(TypographyFeatureInfo info, bool previewOnly = false)
        {
            if (ViewModel.IsLoadingCharacters || ViewModel.Chars == null)
                return;
            
            if (CharGrid.ItemsSource != null && CharGrid.ItemsPanelRoot != null)
            {
                ViewModel.SelectedCharTypography = info;
                IXamlDirectObject p = _xamlDirect.GetXamlDirectObject(TxtPreview);
                CharacterGridView.UpdateTypography(_xamlDirect, p, info);
            }

            if (CopySequenceText != null && !previewOnly)
            {
                IXamlDirectObject p = _xamlDirect.GetXamlDirectObject(CopySequenceText);
                CharacterGridView.UpdateTypography(_xamlDirect, p, info);
            }
        }

        Visibility GetAlternatesVis(TypographyFeatureInfo global, List<TypographyFeatureInfo> info)
        {
            Visibility vis = info == null || info.Count <= 1 ? Visibility.Collapsed : Visibility.Visible;

            void Update()
            {
                if (ViewModel.SelectedCharVariations != null)
                {
                    // The character might not support the current ViewModel typography, so make sure we fallback
                    // too an appropriate selection
                    if (ViewModel.SelectedCharVariations.Contains(global))
                        PreviewTypographySelector.SelectedItem = global;
                    else
                        PreviewTypographySelector.SelectedItem = ViewModel.SelectedCharVariations.FirstOrDefault();
                }
            }

            Update();
            // Hack to ensure properly set when switching between characters in a font
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, Update);
            
            return vis;
        }

        


        /* Composition */

        public void PlayFontChanged(bool withHeader = true)
        {
            /* Create the animation that is played upon changing font */
            if (ResourceHelper.AllowAnimation)
            {
                int offset = 0;
                if (withHeader)
                {
                    offset = 83;
                    CompositionFactory.PlayEntrance(CharGridHeader, 83);
                }

                if (ViewModel.DisplayMode == FontDisplayMode.CharacterMap)
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
                else if (ViewModel.DisplayMode == FontDisplayMode.TypeRamp)
                {
                    CompositionFactory.PlayEntrance(TypeRampInputRow, offset * 2);

                    if (TypeRampList != null)
                    {
                        List<UIElement> items = new() { VariableAxis };
                        items.AddRange(TypeRampList.TryGetChildren());
                        CompositionFactory.PlayEntrance(items, (offset * 2) + 34);
                    }
                }
            }
        }

        private void CopySequenceRoot_Loading(FrameworkElement sender, object args)
        {
            //CopySequenceRoot.SetHideAnimation(CompositionFactory.CreateSlideOutY(sender));
            //CopySequenceRoot.SetShowAnimation(CompositionFactory.CreateSlideIn(sender));

            CopySequenceRoot.SetTranslation(new Vector3(0, (float)CopySequenceRoot.Height, 0));
            CopySequenceRoot.GetElementVisual().StartAnimation(CompositionFactory.TRANSLATION, CompositionFactory.CreateSlideIn(sender));

            //Composition.SetThemeShadow(CopySequenceRoot, 20, CharGrid);
        }
    }


    public partial class FontMapView
    {
        public static async Task CreateNewViewForFontAsync(InstalledFont font, StorageFile sourceFile = null, CharacterRenderingOptions options = null)
        {
            void CreateView()
            {
                FontItem item = new FontItem(font);
                item.Selected = options?.Variant ?? font.DefaultVariant;

                FontMapView map = new() { 
                    IsStandalone = true, 
                    ViewModel = { SelectedFont = item, IsExternalFile = sourceFile != null, SourceFile = sourceFile } };

                // Attempt to apply any custom rendering options from the source view
                if (options != null && options.Variant != null && font.Variants.Contains(options.Variant))
                {
                    if (options.DefaultTypography != null)
                        map.ViewModel.SelectedTypography = options.DefaultTypography;
                }

                Window.Current.Content = map;
                Window.Current.Activate();
            }

            var view = await WindowService.CreateViewAsync(CreateView, false);
            await WindowService.TrySwitchToWindowAsync(view, false);
        }
    }
}
