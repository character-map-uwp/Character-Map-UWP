using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using CommonServiceLocator;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Views;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using CharacterMap.Models;
using Microsoft.Toolkit.Uwp.UI.Controls;

namespace CharacterMap.Views
{
    public sealed partial class FontMapView : UserControl, IInAppNotificationPresenter
    {
        #region Dependency Properties

        #region Font

        public InstalledFont Font
        {
            get => (InstalledFont)GetValue(FontProperty);
            set => SetValue(FontProperty, value);
        }

        public static readonly DependencyProperty FontProperty =
            DependencyProperty.Register(nameof(Font), typeof(InstalledFont), typeof(FontMapView), new PropertyMetadata(null, (d, e) =>
            {
                if (d is FontMapView f)
                    f.ViewModel.SelectedFont = e.NewValue as InstalledFont;
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

        #endregion

        public bool IsStandalone { get; set; }

        private XamlDirect _xamlDirect { get; set; }

        private long _previewColumnToken = long.MinValue;

        public FontMapView()
        {
            InitializeComponent();
            Loading += FontMapView_Loading;
            Loaded += FontMapView_Loaded;
            Unloaded += FontMapView_Unloaded;

            ViewModel = new FontMapViewModel(
                ServiceLocator.Current.GetInstance<IDialogService>(), 
                ResourceHelper.Get<AppSettings>(nameof(AppSettings)));

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            CharGrid.SetDesiredContainerUpdateDuration(TimeSpan.FromSeconds(1.5));
        }

        private void FontMapView_Loading(FrameworkElement sender, object args)
        {
            _xamlDirect = XamlDirect.GetDefault();

            if (IsStandalone)
            {
                ApplicationView.GetForCurrentView()
                    .SetDesiredBoundsMode(ApplicationViewBoundsMode.UseVisible);

                Window.Current.Activate();
                Window.Current.Closed += Current_Closed;
            }
        }

        private void FontMapView_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            Messenger.Default.Register<GridSizeUpdatedMessage>(this, _ => UpdateDisplay());
            Messenger.Default.Register<AppNotificationMessage>(this, OnNotificationMessage);

            PreviewColumn.Width = new GridLength(ViewModel.Settings.LastColumnWidth);
            _previewColumnToken = PreviewColumn.RegisterPropertyChangedCallback(ColumnDefinition.WidthProperty, (d, r) =>
            {
                ViewModel.Settings.LastColumnWidth = PreviewColumn.Width.Value;
            });
        }

        private void FontMapView_Unloaded(object sender, RoutedEventArgs e)
        {
            PreviewColumn.UnregisterPropertyChangedCallback(ColumnDefinition.WidthProperty, _previewColumnToken);
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            Messenger.Default.Unregister(this);
        }

        private void Current_Closed(object sender, Windows.UI.Core.CoreWindowEventArgs e)
        {
            this.Bindings.StopTracking();
            Window.Current.Closed -= Current_Closed;
            Window.Current.Content = null;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.SelectedFont))
            {
                UpdateStates();
            }
            else if (e.PropertyName == nameof(ViewModel.SelectedVariant))
            {
                _ = SetCharacterSelectionAsync();
            }
            else if (e.PropertyName == nameof(ViewModel.ShowColorGlyphs))
            {
                UpdateColorsFonts(ViewModel.ShowColorGlyphs);
            }
            else if (e.PropertyName == nameof(ViewModel.SelectedTypography))
            {
                UpdateTypographies(ViewModel.SelectedTypography);
            }
            else if (e.PropertyName == nameof(ViewModel.SelectedChar))
            {
                if (_xamlDirect == null)
                    return;

                Composition.PlayScaleEntrance(TxtPreview, .85f, 1f);

                IXamlDirectObject p = _xamlDirect.GetXamlDirectObject(TxtPreview);
                UpdateTypography(p, ViewModel.SelectedTypography);
            }
            else if (e.PropertyName == nameof(ViewModel.Chars))
            {
                CharGrid.ItemsSource = ViewModel.Chars;
                Composition.PlayEntrance(CharGrid);
            }
        }

        private void UpdateStates()
        {
            // Ideally should have been achieved with VisualState setters, buuuuut didn't work for some reason
            VisualStateManager.GoToState(this, ViewModel.SelectedFont == null ? NoFontState.Name : HasFontState.Name,
                true);
        }




        /* Public surface-area methods */

        public void SelectCharacter(Character ch)
        {
            if (null != ch)
            {
                CharGrid.SelectedItem = ch;
                CharGrid.ScrollIntoView(ch);
            }
        }

        public string UpdateStatusBarLabel(FontVariant variant)
        {
            if (variant == null)
                return string.Empty;

            return Localization.Get("StatusBarCharacterCount", variant.Characters.Count);
        }

        public void TryCopy()
        {
            if (CharGrid.SelectedItem is Character character &&
                !TxtSymbolIcon.SelectedText.Any() &&
                !TxtFontIcon.SelectedText.Any() &&
                !TxtXamlCode.SelectedText.Any())
            {
                TryCopyInternal();
            }
        }

        private void TryCopyInternal()
        {
            if (CharGrid.SelectedItem is Character character)
            {
                DataPackage dp = new DataPackage
                {
                    RequestedOperation = DataPackageOperation.Copy,
                };
                dp.SetText(character.Char);

                if (!ViewModel.SelectedVariant.IsImported)
                {
                    // We can allow users to also copy the glyph with the font metadata included,
                    // so when they paste into a supported program like Microsoft Word or 
                    // Adobe Photoshop the correct font is automatically applied to the paste.
                    // To do so we need to create a RichTextFormat document, and we use the 
                    // RichEditBox as a proxy to do this (otherwise the syntax is arkane).
                    // This won't include any Typographic variations unfortunately.
                    RichEditBox r = new RichEditBox();
                    ITextCharacterFormat format = r.TextDocument.GetDefaultCharacterFormat();
                    format.Size = 12;
                    format.Name = ViewModel.FontFamily.Source;
                    r.TextDocument.SetDefaultCharacterFormat(format);
                    r.TextDocument.SetText(TextSetOptions.None, character.Char);
                    r.TextDocument.GetText(TextGetOptions.FormatRtf, out string doc);

                    dp.SetRtf(doc);
                    dp.SetHtmlFormat($"<p style=\"font-family:'{ViewModel.FontFamily.Source}'; \">{character.Char}</p>");
                }

                Clipboard.SetContent(dp);
            }
            BorderFadeInStoryboard.Begin();
        }




        /* UI Event Handlers */

        private void BtnSaveAs_OnClick(object sender, RoutedEventArgs e)
        {
            SaveAsCommandBar.IsOpen = !SaveAsCommandBar.IsOpen;
        }

        private void BtnSaveAsSvg_OnClick(object sender, RoutedEventArgs e)
        {
            SaveAsSvgCommandBar.IsOpen = !SaveAsSvgCommandBar.IsOpen;
        }

        private void TxtFontIcon_OnGotFocus(object sender, RoutedEventArgs e)
        {
            TxtFontIcon.SelectAll();
        }

        private void TxtXamlCode_OnGotFocus(object sender, RoutedEventArgs e)
        {
            TxtXamlCode.SelectAll();
        }

        private void BtnCopyXamlCode_OnClick(object sender, RoutedEventArgs e)
        {
            Edi.UWP.Helpers.Utils.CopyToClipBoard(TxtXamlCode.Text.Trim());
            BorderFadeInStoryboard.Begin();
        }

        private void BtnCopyFontIcon_OnClick(object sender, RoutedEventArgs e)
        {
            Edi.UWP.Helpers.Utils.CopyToClipBoard(TxtFontIcon.Text.Trim());
            BorderFadeInStoryboard.Begin();
        }

        private void TxtSymbolIcon_OnGotFocus(object sender, RoutedEventArgs e)
        {
            TxtSymbolIcon.SelectAll();
        }

        private void BtnCopySymbolIcon_OnClick(object sender, RoutedEventArgs e)
        {
            Edi.UWP.Helpers.Utils.CopyToClipBoard(TxtSymbolIcon.Text.Trim());
            BorderFadeInStoryboard.Begin();
        }

        private void PreviewGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var newSize = e.NewSize.Width - 2;

            foreach (AppBarButton item in SaveAsCommandBar.SecondaryCommands.Concat(SaveAsSvgCommandBar.SecondaryCommands))
            {
                item.Width = newSize;
            }
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
            if (sender is MenuFlyout menu && ViewModel.SelectedFont is InstalledFont font)
            {
                FlyoutHelper.CreateMenu(
                    menu,
                    font,
                    IsStandalone);
            }
        }

        private Visibility ShowFilePath(string filePath, bool isImported)
        {
            if (!isImported && !string.IsNullOrWhiteSpace(filePath))
                return Visibility.Visible;

            return Visibility.Collapsed;
        }

        private Task SetCharacterSelectionAsync()
        {
            return Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () =>
            {
                if (null != CharGrid.SelectedItem)
                {
                    CharGrid.ScrollIntoView(ViewModel.SelectedChar, ScrollIntoViewAlignment.Default);
                }
            }).AsTask();
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
            // We apply the size changes by clearing the ItemsSource and resetting it,
            // allowing the GridView to re-layout all of it's items with their new size.
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                if (!this.IsLoaded)
                    return;

                CharGrid.ItemsSource = null;
                await Task.Yield();
                CharGrid.ItemsSource = ViewModel.Chars;
                ViewModel.SetDefaultChar();
                _ = SetCharacterSelectionAsync();
            });
        }

        private void CharGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
                return;

            /* 
             * For performance reasons, we've forgone XAML bindings and
             * will update everything in code 
             */
            if (args.ItemContainer is GridViewItem item)
            {
                Character c = ((Character)args.Item);
                UpdateContainer(item, c);
                args.Handled = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void UpdateContainer(GridViewItem item, Character c)
        {
            // Perf considerations:
            // 1 - Batch rendering updates by suspending rendering until all properties are set
            // 2 - Use XAML direct to set new properties, rather than through DP's

            XamlBindingHelper.SuspendRendering(item);

            double size = ViewModel.Settings.GridSize;
            IXamlDirectObject go = _xamlDirect.GetXamlDirectObject(item.ContentTemplateRoot);

            _xamlDirect.SetDoubleProperty(go, XamlPropertyIndex.FrameworkElement_Width, size);
            _xamlDirect.SetDoubleProperty(go, XamlPropertyIndex.FrameworkElement_Height, size);

            IXamlDirectObject cld = _xamlDirect.GetXamlDirectObjectProperty(go, XamlPropertyIndex.Panel_Children);
            IXamlDirectObject o = _xamlDirect.GetXamlDirectObjectFromCollectionAt(cld, 0);

            _xamlDirect.SetObjectProperty(o, XamlPropertyIndex.TextBlock_FontFamily, ViewModel.FontFamily);
            _xamlDirect.SetEnumProperty(o, XamlPropertyIndex.TextBlock_FontStretch, (uint)ViewModel.SelectedVariant.FontFace.Stretch);
            _xamlDirect.SetEnumProperty(o, XamlPropertyIndex.TextBlock_FontStyle, (uint)ViewModel.SelectedVariant.FontFace.Style);
            _xamlDirect.SetObjectProperty(o, XamlPropertyIndex.TextBlock_FontWeight, ViewModel.SelectedVariant.FontFace.Weight);
            _xamlDirect.SetBooleanProperty(o, XamlPropertyIndex.TextBlock_IsColorFontEnabled, ViewModel.ShowColorGlyphs);
            _xamlDirect.SetDoubleProperty(o, XamlPropertyIndex.TextBlock_FontSize, size / 2d);

            UpdateColorFont(null, o, ViewModel.ShowColorGlyphs);
            UpdateTypography(o, ViewModel.SelectedTypography);

            _xamlDirect.SetStringProperty(o, XamlPropertyIndex.TextBlock_Text, c.Char);

            IXamlDirectObject o2 = _xamlDirect.GetXamlDirectObjectFromCollectionAt(cld, 1);
            _xamlDirect.SetStringProperty(o2, XamlPropertyIndex.TextBlock_Text, c.UnicodeString);

            XamlBindingHelper.ResumeRendering(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void UpdateColorFont(TextBlock block, IXamlDirectObject xd, bool value)
        {
            if (xd != null)
                _xamlDirect.SetBooleanProperty(xd, XamlPropertyIndex.TextBlock_IsColorFontEnabled, value);
            else
                block.IsColorFontEnabled = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void UpdateTypography(IXamlDirectObject o, TypographyFeatureInfo info)
        {
            CanvasTypographyFeatureName f = info == null ? CanvasTypographyFeatureName.None : info.Feature;
            TypographyBehavior.SetTypography(o, f, _xamlDirect);
        }

        void UpdateColorsFonts(bool value)
        {
            if (ViewModel.IsLoadingCharacters || CharGrid.ItemsSource == null || CharGrid.ItemsPanelRoot == null)
                return;

            foreach (GridViewItem item in CharGrid.ItemsPanelRoot.Children.Cast<GridViewItem>())
            {
                Grid g = (Grid)item.ContentTemplateRoot;
                TextBlock tb = (TextBlock)g.Children[0];
                UpdateColorFont(tb, null, value);
            }
        }

        void UpdateTypographies(TypographyFeatureInfo info)
        {
            if (ViewModel.IsLoadingCharacters || CharGrid.ItemsSource == null || CharGrid.ItemsPanelRoot == null)
                return;

            IXamlDirectObject p = _xamlDirect.GetXamlDirectObject(TxtPreview);
            UpdateTypography(p, info);

            foreach (GridViewItem item in CharGrid.ItemsPanelRoot.Children.Cast<GridViewItem>())
            {
                Grid g = (Grid)item.ContentTemplateRoot;
                TextBlock tb = (TextBlock)g.Children[0];
                IXamlDirectObject o = _xamlDirect.GetXamlDirectObject(tb);
                UpdateTypography(o, info);
            }
        }




        /* Composition */

        private async void DetailsGrid_Loaded(object sender, RoutedEventArgs e)
        {
            // This avoids strange animation on secondary window
            await Task.Delay(500);
            if (this.IsLoaded)
                Composition.SetStandardReposition(sender, e);
        }

        private void TitleGrid_Loading(FrameworkElement sender, object args)
        {
            Composition.SetThemeShadow(sender, 20, MainUIGrid);
        }

        private void GridSplitter_Loading(FrameworkElement sender, object args)
        {
            Composition.SetThemeShadow(sender, 20, ShadowTarget);
        }
    }


    public partial class FontMapView
    {
        public static async Task CreateNewViewForFontAsync(InstalledFont font, StorageFile sourceFile = null)
        {
            void CreateView()
            {
                FontMapView map = new FontMapView { 
                    IsStandalone = true, 
                    ViewModel = { SelectedFont = font, IsExternalFile = sourceFile != null, SourceFile = sourceFile } }; 
                Window.Current.Content = map;
                Window.Current.Activate();
            }

            var view = await WindowService.CreateViewAsync(CreateView, false);
            await WindowService.TrySwitchToWindowAsync(view, false);
        }
    }
}
