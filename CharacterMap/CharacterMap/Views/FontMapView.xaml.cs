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
using System.ComponentModel;
using Windows.UI.Core;
using CharacterMap.Annotations;
using CharacterMap.Controls;

namespace CharacterMap.Views
{
    public sealed partial class FontMapView : ViewBase, IInAppNotificationPresenter, IPrintPresenter
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

        #region StatusBarLeftContent

        public object StatusBarLeftContent
        {
            get { return (object)GetValue(StatusBarLeftContentProperty); }
            set { SetValue(StatusBarLeftContentProperty, value); }
        }

        public static readonly DependencyProperty StatusBarLeftContentProperty =
            DependencyProperty.Register(nameof(StatusBarLeftContent), typeof(object), typeof(FontMapView), new PropertyMetadata(null));

        #endregion

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

        public object ThemeLock { get; } = new object();

        private Debouncer _sizeDebouncer { get; } = new Debouncer();

        private XamlDirect _xamlDirect { get; }

        private long _previewColumnToken = long.MinValue;

        public FontMapView()
        {
            RequestedTheme = ResourceHelper.AppSettings.UserRequestedTheme;

            InitializeComponent();
            Loading += FontMapView_Loading;
            Loaded += FontMapView_Loaded;
            Unloaded += FontMapView_Unloaded;

            ViewModel = new FontMapViewModel(
                ServiceLocator.Current.GetInstance<IDialogService>(), 
                ResourceHelper.AppSettings);

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            CharGrid.ItemSize = ViewModel.Settings.GridSize;
            CharGrid.SetDesiredContainerUpdateDuration(TimeSpan.FromSeconds(1.5));
            _xamlDirect = XamlDirect.GetDefault();
        }

        private void FontMapView_Loading(FrameworkElement sender, object args)
        {
            if (IsStandalone)
            {
                ApplicationView.GetForCurrentView()
                    .SetDesiredBoundsMode(ApplicationViewBoundsMode.UseVisible);

                Window.Current.Activate();
                Window.Current.Closed -= Current_Closed;
                Window.Current.Closed += Current_Closed;

                LayoutRoot.KeyDown -= LayoutRoot_KeyDown;

                LayoutRoot.KeyDown += LayoutRoot_KeyDown;
            }
        }

        private void FontMapView_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            Messenger.Default.Register<AppNotificationMessage>(this, OnNotificationMessage);
            Messenger.Default.Register<AppSettingsChangedMessage>(this, OnAppSettingsChanged);
            Messenger.Default.Register<PrintRequestedMessage>(this, m =>
            {
                if (Dispatcher.HasThreadAccess)
                    TryPrint();
            });

            UpdateSearchStates();
            UpdateCharacterFit();

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

            LayoutRoot.KeyDown -= LayoutRoot_KeyDown;

            Messenger.Default.Unregister(this);
        }

        private void Current_Closed(object sender, CoreWindowEventArgs e)
        {
            this.Bindings.StopTracking();
            Window.Current.Closed -= Current_Closed;
            Window.Current.Content = null;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.SelectedFont))
            {
                UpdateStates();
            }
            else if (e.PropertyName == nameof(ViewModel.SelectedVariant))
            {
                _ = SetCharacterSelectionAsync();
            }
            else if (e.PropertyName == nameof(ViewModel.SelectedTypography))
            {
                UpdateTypography(ViewModel.SelectedTypography);
            }
            else if (e.PropertyName == nameof(ViewModel.SelectedChar))
            {
                if (ViewModel.Settings.UseSelectionAnimations)
                    Composition.PlayScaleEntrance(TxtPreview, .85f, 1f);

                UpdateTypography(ViewModel.SelectedTypography);
            }
            else if (e.PropertyName == nameof(ViewModel.Chars))
            {
                CharGrid.ItemsSource = ViewModel.Chars;

                if (ViewModel.Settings.UseSelectionAnimations)
                    Composition.PlayEntrance(CharGrid);
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
                    case nameof(AppSettings.DevToolsLanguage):
                        ViewModel.UpdateDevValues();
                        break;
                    case nameof(AppSettings.GridSize):
                        UpdateDisplay();
                        break;
                    case nameof(AppSettings.UserRequestedTheme):
                        this.RequestedTheme = ViewModel.Settings.UserRequestedTheme;
                        OnPropertyChanged(nameof(ThemeLock));
                        break;
                    case nameof(AppSettings.UseInstantSearch):
                        UpdateSearchStates();
                        break;
                    case nameof(AppSettings.FitCharacter):
                        UpdateCharacterFit();
                        break;
                }
            });
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
                VisualStateManager.GoToState(
                  this,
                  ViewModel.Settings.UseInstantSearch ? nameof(InstantSearchState) : nameof(ManualSearchState),
                  true);
            }
        }

        private void LayoutRoot_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            var ctrlState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Control);
            if ((ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
            {
                switch (e.Key)
                {
                    case VirtualKey.C:
                        TryCopy();
                        break;
                    case VirtualKey.P:
                        Messenger.Default.Send(new PrintRequestedMessage());
                        break;
                }
            }
        }

        private void UpdateStates()
        {
            // Ideally should have been achieved with VisualState setters, buuuuut didn't work for some reason
            VisualStateManager.GoToState(
                this, 
                ViewModel.SelectedFont == null ? NoFontState.Name : HasFontState.Name,
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
                (TxtSymbolIcon == null || !TxtSymbolIcon.SelectedText.Any()) &&
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
                    // We can allow users to also copy the glyph with the font meta-data included,
                    // so when they paste into a supported program like Microsoft Word or 
                    // Adobe Photoshop the correct font is automatically applied to the paste.
                    // This can't include any Typographic variations unfortunately.

                    var rtf = $@"{{\rtf1\fbidis\ansi\ansicpg1252\deff0\nouicompat\deflang2057{{\fonttbl{{\f0\fnil {ViewModel.FontFamily.Source};}}}} " +
                               $@"{{\colortbl;\red0\green0\blue0; }}\viewkind4\uc1\pard\ltrpar\tx720\cf1\f0\fs24\u{character.UnicodeIndex}?}}";
                    dp.SetRtf(rtf);

                    var longName = ViewModel.FontFamily.Source;
                    if (ViewModel.SelectedVariant.FontInformation.FirstOrDefault(i => i.Key == "Full Name") is var p)
                    {
                        if (p.Value != longName)
                            longName = $"{ViewModel.FontFamily.Source}, {p.Value}";
                    }
                    dp.SetHtmlFormat($"<p style=\"font-family:'{longName}'; \">{character.Char}</p>");
                }

                Clipboard.SetContent(dp);
            }
            BorderFadeInStoryboard.Begin();
        }




        /* UI Event Handlers */

        private void BtnFit_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Settings.FitCharacter = !ViewModel.Settings.FitCharacter;
        }

        private void OnCopyGotFocus(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        private void BtnCopyXamlCode_OnClick(object sender, RoutedEventArgs e)
        {
            Utils.CopyToClipBoard(TxtXamlCode.Text.Trim());
            BorderFadeInStoryboard.Begin();
        }

        private void BtnCopyFontIcon_OnClick(object sender, RoutedEventArgs e)
        {
            Utils.CopyToClipBoard(TxtFontIcon.Text.Trim());
            BorderFadeInStoryboard.Begin();
        }

        private void BtnCopyXamlPath_OnClick(object sender, RoutedEventArgs e)
        {
            GeometryFlyout?.Hide();
            Utils.CopyToClipBoard(ViewModel.XamlPathGeom);
            BorderFadeInStoryboard.Begin();
        }

        private void BtnCopySymbolIcon_OnClick(object sender, RoutedEventArgs e)
        {
            Utils.CopyToClipBoard(TxtSymbolIcon.Text.Trim());
            BorderFadeInStoryboard.Begin();
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

                var size = (int)CharGrid.ActualWidth + (int)Splitter.ActualWidth + (int)PreviewGrid.ActualWidth;
                if (this.ActualWidth < size && this.ActualWidth < 700)
                {
                    PreviewColumn.Width = new GridLength((int)(this.ActualWidth - CharGrid.ActualWidth - Splitter.ActualWidth));
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
                    IsStandalone,
                    true);
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
            return Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                if (null != CharGrid.SelectedItem)
                {
                    CharGrid.ScrollIntoView(ViewModel.SelectedChar, ScrollIntoViewAlignment.Default);
                }
            }).AsTask();
        }




        /* Print Helpers */

        FontMapView IPrintPresenter.GetFontMap() => this;

        Border IPrintPresenter.GetPresenter()
        {
            this.FindName(nameof(PrintPresenter));
            return PrintPresenter;
        }

        public GridLength GetTitleBarHeight() => TitleBar.TemplateSettings.GridHeight;

        private void TryPrint()
        {
            if (this.GetFirstAncestorOfType<MainPage>() is null)
            {
                PrintView.Show(this);
            }
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

            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
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

        void UpdateTypography(TypographyFeatureInfo info)
        {
            if (ViewModel.IsLoadingCharacters || CharGrid.ItemsSource == null || CharGrid.ItemsPanelRoot == null)
                return;

            IXamlDirectObject p = _xamlDirect.GetXamlDirectObject(TxtPreview);
            CharacterGridView.UpdateTypography(_xamlDirect, p, info);
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
            //Composition.SetThemeShadow(sender, 20, MainUIGrid);
        }

        private void GridSplitter_Loading(FrameworkElement sender, object args)
        {
            //Composition.SetThemeShadow(sender, 20, ShadowTarget);
        }

        private void MenuFlyout_Opening_1(object sender, object e)
        {
            if (sender is MenuFlyout flyout
                && flyout.GetPresenter() is MenuFlyoutPresenter p)
            {
                //flyout.GetPresenter().Width = flyout.Target.ActualWidth;
                //foreach (var item in flyout.Items)
                //    item.Width = flyout.Target.ActualWidth;
            }
        }

        private void SaveAsFlyout_Opened(object sender, object e)
        {
            if (sender is MenuFlyout flyout)
            {
                flyout.GetPresenter().Width = flyout.Target.ActualWidth;
                //foreach (var item in flyout.Items)
                //    item.Width = flyout.Target.ActualWidth;
            }
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
