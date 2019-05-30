using CharacterMap.Core;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using CommonServiceLocator;
using GalaSoft.MvvmLight.Views;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace CharacterMap.Views
{
    public sealed partial class FontMapView : UserControl
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

        #region IsStandalone

        public bool IsStandalone
        {
            get => (bool)GetValue(IsStandaloneProperty);
            set => SetValue(IsStandaloneProperty, value);
        }

        public static readonly DependencyProperty IsStandaloneProperty =
            DependencyProperty.Register(nameof(IsStandalone), typeof(bool), typeof(FontMapView), new PropertyMetadata(false));

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

        public AppSettings Settings { get; }

        public FontMapView()
        {
            InitializeComponent();
            Loading += FontMapView_Loading;
            Settings = (AppSettings)App.Current.Resources[nameof(AppSettings)];
            ViewModel = new FontMapViewModel(ServiceLocator.Current.GetInstance<IDialogService>());
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void FontMapView_Loading(FrameworkElement sender, object args)
        {
            if (IsStandalone)
            {
                ApplicationView.GetForCurrentView()
                    .SetDesiredBoundsMode(ApplicationViewBoundsMode.UseVisible);
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.SelectedFont))
            {
                UpdateStates();

                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () =>
                {
                    if (null != CharGrid.SelectedItem)
                    {
                        CharGrid.ScrollIntoView(CharGrid.SelectedItem, ScrollIntoViewAlignment.Default);
                    }
                });
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

        public void TryCopy()
        {
            if (CharGrid.SelectedItem is Character character &&
                !TxtSymbolIcon.SelectedText.Any() &&
                !TxtFontIcon.SelectedText.Any() &&
                !TxtXamlCode.SelectedText.Any())
            {
                Edi.UWP.Helpers.Utils.CopyToClipBoard(character.Char);
                BorderFadeInStoryboard.Begin();
            }
        }



        /* UI Event Handlers */

        private void BtnCopy_OnClick(object sender, RoutedEventArgs e)
        {
            if (CharGrid.SelectedItem is Character character)
            {
                var dp = new DataPackage
                {
                    RequestedOperation = DataPackageOperation.Copy,
                };
                dp.SetText(character.Char);
                Clipboard.SetContent(dp);
            }
            BorderFadeInStoryboard.Begin();
        }

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

        internal void OnSearchBoxGotFocus(AutoSuggestBox searchBox)
        {
            if (ViewModel.SearchResults != null && ViewModel.SearchResults.Count > 0)
            {
                searchBox.IsSuggestionListOpen = true;
            }
            else
            {
                if (Utils.IsSystemOnWin10v1809OrNewer)
                {
                    if (!searchBox.ContextFlyout.IsOpen && string.IsNullOrWhiteSpace(ViewModel.SearchQuery))
                    {
                        searchBox.ContextFlyout.ShowAt(searchBox, new FlyoutShowOptions
                        {
                            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                            ShowMode = FlyoutShowMode.Transient
                        });
                    }
                }

                if (!string.IsNullOrWhiteSpace(ViewModel.SearchQuery))
                {
                    ViewModel.DebounceSearch(ViewModel.SearchQuery, Settings.InstantSearchDelay);
                }
            }
        }

        internal void OnSearchBoxSubmittedQuery(AutoSuggestBox searchBox)
        {
            // commented below line because it will keep search result list open even when user selected an item in search result
            // searchBox.IsSuggestionListOpen = true;
            if (!string.IsNullOrWhiteSpace(ViewModel.SearchQuery))
            {
                ViewModel.DebounceSearch(ViewModel.SearchQuery, Settings.InstantSearchDelay, SearchSource.ManualSubmit);
            }
        }

        internal void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is IGlyphData data)
            {
                SelectCharacter(ViewModel.Chars.First(c => c.UnicodeIndex == data.UnicodeIndex));
            }
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            OnSearchBoxGotFocus(SearchBox);
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
                e.Handled = true;
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            OnSearchBoxSubmittedQuery(SearchBox);
        }
    }

    public partial class FontMapView
    {
        public static async Task CreateNewViewForFontAsync(InstalledFont font)
        {
            void CreateView()
            {
                FontMapView map = new FontMapView { IsStandalone = true, ViewModel = { SelectedFont = font } };
                Window.Current.Content = map;
                Window.Current.Activate();
            }

            var view = await WindowService.CreateViewAsync(CreateView, false);
            await WindowService.TrySwitchToWindowAsync(view, false);
        }
    }
}
