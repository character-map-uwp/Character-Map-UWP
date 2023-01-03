using CharacterMap.Controls;
using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Views
{
    public sealed partial class MainPage : ViewBase, IInAppNotificationPresenter, IPopoverPresenter
    {
        public static CoreDispatcher MainDispatcher { get; private set; }

        public MainViewModel ViewModel { get; }

        private Debouncer _fontListDebouncer { get; } = new Debouncer();

        private UISettings _uiSettings { get; }

        private ICommand FilterCommand { get; }

        private ICommand CollectionSelectedCommand { get; }

        public MainPage() : this(null) { }

        public MainPage(MainViewModelArgs args)
        {
            InitializeComponent();

            // If ViewModel is null, then this is the "primary" application Window
            if (args is null)
            {
                ViewModel = Ioc.Default.GetService<MainViewModel>();
                MainDispatcher = Dispatcher;
            }
            else
            {
                ViewModel = new MainViewModel(args);
            }

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            Register<CollectionsUpdatedMessage>(OnCollectionsUpdated);
            Register<AppSettingsChangedMessage>(OnAppSettingsChanged);
            Register<ModalClosedMessage>(m =>
            {
                if (Dispatcher.HasThreadAccess)
                    OnModalClosed();
            });
            Register<PrintRequestedMessage>(m =>
            {
                if (Dispatcher.HasThreadAccess)
                {
                    PrintView.Show(this);
                    
                    // NB: Printing works by using an off-screen canvas inside
                    //     the FontMapView. OnModelOpened hides this canvas
                    //     preventing printing from working. So in the case of 
                    //     printing, we do *not* hide the FontMap
                    OnModalOpened(false);
                }
            });
            Register<ExportRequestedMessage>(m =>
            {
                if (Dispatcher.HasThreadAccess)
                {
                    ExportView.Show(this);
                    OnModalOpened();
                }
            });

            this.SizeChanged += MainPage_SizeChanged;

            _uiSettings = new UISettings();
            _uiSettings.ColorValuesChanged += OnColorValuesChanged;

            FilterCommand = new RelayCommand<object>(e => OnFilterClick(e));
            CollectionSelectedCommand = new RelayCommand<object>(e =>
            {
                if (!FontsSemanticZoom.IsZoomedInViewActive)
                    FontsSemanticZoom.IsZoomedInViewActive = true;

                ViewModel.SelectedCollection = e as UserFontCollection;
            });
        }

        bool _disableMapChange = true;

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.GroupedFontList):
                    if (ViewModel.IsLoadingFonts)
                        return;

                    if (ViewModel.Settings.UseSelectionAnimations 
                        && !ViewModel.IsSearchResults)
                    {
                        CompositionFactory.PlayEntrance(LstFontFamily, 66, 100);
                        CompositionFactory.PlayEntrance(GroupLabel, 0, 0, 80);
                        if (InlineLabelCount.Visibility == Visibility.Visible)
                            CompositionFactory.PlayEntrance(InlineLabelCount, 83, 0, 80);
                    }
                    break;

                case nameof(ViewModel.SelectedFont):
                    if (ViewModel.SelectedFont != null)
                    {
                        SetSelectedItem(ViewModel.SelectedFont);
                        if (ViewModel.TabIndex >= 0 && !ViewModel.IsCreating)
                        {
                            ViewModel.Fonts[ViewModel.TabIndex].SetFont(ViewModel.SelectedFont);
                            FontMap.Font = ViewModel.Fonts[ViewModel.TabIndex];

                            if (_disableMapChange)
                                _disableMapChange = false;
                            else
                                FontMap.PlayFontChanged();
                        }
                    }
                    break;

                case nameof(ViewModel.TabIndex):
                    if (ViewModel.TabIndex > -1)
                    {
                        if (ViewModel.Fonts[ViewModel.TabIndex].Font is InstalledFont font 
                            && LstFontFamily.SelectedItem as InstalledFont != font)
                        {
                            _disableMapChange = true;
                            SetSelectedItem(font);
                            StartScrollSelectedIntoView(); // Scrolls font into view
                        }
                    }
                    break;

                case nameof(ViewModel.FontSearch):
                case nameof(ViewModel.IsSearchResults):
                    // Required to prevent crash with SemanticZoom when there
                    // are no items in the results list
                    if (!FontsSemanticZoom.IsZoomedInViewActive)
                        FontsSemanticZoom.IsZoomedInViewActive = true;
                    break;

                case nameof(ViewModel.IsLoadingFonts):
                case nameof(ViewModel.IsLoadingFontsFailed):
                    UpdateLoadingStates();
                    break;
            }
        }

        void SetSelectedItem(InstalledFont font)
        {
            LstFontFamily.SelectedItem = font;
            // Required for tabs with fonts not in FontList
            if (LstFontFamily.SelectedItem as InstalledFont != font)
                LstFontFamily.SelectedItem = null;
        }

        private void FontMapContainer_AddTabButtonClick(TabView sender, object args)
        {
            ViewModel.Fonts.Add(new (ViewModel.SelectedFont));
            FontsTabBar.SelectedIndex = ViewModel.Fonts.Count - 1;
        }

        private void FontMapContainer_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            ViewModel.TryCloseTab(sender.TabItems.IndexOf(args.Item));
        }

        private void TabViewItem_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement f && f.DataContext is FontItem item)
                ViewModel.TryCloseTab(FontsTabBar.TabItems.IndexOf(item));
        }

        private void OnAppSettingsChanged(AppSettingsChangedMessage msg)
        {
            switch (msg.PropertyName)
            {
                case nameof(AppSettings.UseFontForPreview):
                    OnFontPreviewUpdated();
                    break;
            }
        }

        protected override void OnLoaded(object sender, RoutedEventArgs e)
        {
            Register<ImportMessage>(OnFontImportRequest);
            Register<AppNotificationMessage>(OnNotificationMessage);

            ViewModel.FontListCreated -= ViewModel_FontListCreated;
            ViewModel.FontListCreated += ViewModel_FontListCreated;

            UpdateLoadingStates();
            
            FontMap.ViewModel.Folder = ViewModel.Folder;

            if (DesignMode)
            {
                FontsTabBar.TabItemsSource = new List<FontItem> { new(), new(), new() };
            }
        }

        protected override void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsSecondaryView)
            {
                // For Secondary Views, cleanup EVERYTHING to allow the view to get
                // dropped from memory
                _uiSettings.ColorValuesChanged -= OnColorValuesChanged;
                Messenger.UnregisterAll(this);
                this.Bindings.StopTracking();
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                this.FontMap.Cleanup();
            }
            else
            {
                // Primary/Main view might actually be restored at some point, so
                // don't unhook *everything*
                Unregister<ImportMessage>();
                Unregister<AppNotificationMessage>();
            }

            ViewModel.FontListCreated -= ViewModel_FontListCreated;
        }

        private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width < 900)
            {
                VisualStateManager.GoToState(this, nameof(CompactViewState), true);
            }
            else if (ViewStates.CurrentState == CompactViewState)
            {
                VisualStateManager.GoToState(this, nameof(DefaultViewState), true);
            }
        }

        private void OnColorValuesChanged(UISettings settings, object e)
        {
            RunOnUI(() =>
            {
                Messenger.Send(new AppSettingsChangedMessage(nameof(AppSettings.UserRequestedTheme)));
                ResourceHelper.AppSettings.UpdateTheme();
            });
        }

        private void UpdateLoadingStates()
        {
            TitleBar.TryUpdateMetrics();

            if (ViewModel.IsLoadingFonts && !ViewModel.IsLoadingFontsFailed)
                VisualStateManager.GoToState(this, nameof(FontsLoadingState), true);
            else if (ViewModel.IsLoadingFontsFailed)
                VisualStateManager.GoToState(this, nameof(FontsFailedState), true);
            else
            {
                VisualStateManager.GoToState(this, nameof(FontsLoadedState), false);
                if (ViewModel.Settings.UseSelectionAnimations)
                {
                    CompositionFactory.StartStartUpAnimation(
                        new List<FrameworkElement>
                        {
                            OpenFontPaneButton,
                            FontListFilter,
                            OpenFontButton,
                            FontMap.FontTitleBlock,
                            FontMap.SearchBox,
                            BtnSettings
                        },
                        new List<UIElement>
                        {
                            FontListSearchBox,
                            FontsSemanticZoom,
                            FontMap.CharGridHeader,
                            FontMap.SplitterContainer,
                            FontMap.CharGrid,
                            FontMap.PreviewGrid
                        });
                }
            }
        }

        private void ViewModel_FontListCreated(object sender, EventArgs e)
        {
            StartScrollSelectedIntoView();
        }

        void StartScrollSelectedIntoView()
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
            {
                await Task.Delay(50);
                if (LstFontFamily.SelectedItem is not null)
                {
                    LstFontFamily.ScrollIntoView(
                        LstFontFamily.SelectedItem, ScrollIntoViewAlignment.Leading);
                }
            });
        }

        private void TogglePane_Click(object sender, RoutedEventArgs e)
        {
            if (SplitView.DisplayMode == SplitViewDisplayMode.Inline)
            {
                VisualStateManager.GoToState(this, nameof(CollapsedViewState), true);
            }
            else
            {
                VisualStateManager.GoToState(this, nameof(DefaultViewState), true);
            }
        }

        private void BtnSettings_OnClick(object sender, RoutedEventArgs e)
        {
            // Zune theme shows settings button early right now, so to avoid
            // crashing do nothing until fonts are loaded
            if (ViewModel.IsLoadingFonts)
                return; 

            this.FindName(nameof(SettingsView));
            SettingsView.Show(FontMap.ViewModel.SelectedVariant, ViewModel.SelectedFont);
            OnModalOpened();
        }

        private void FontsTabBar_TabDroppedOutside(TabView sender, TabViewTabDroppedOutsideEventArgs args)
        {
            if (args.Item is FontItem font)
                _ = FontMapView.CreateNewViewForFontAsync(font.Font);
        }

        async void OnModalOpened(bool hideFontMap = true)
        {
            // Hide FontMap when a modal is showing to improve the performance
            // of resizing the window (by skipping having to rearrange the character
            // map when it is not actually "visible" on screen.
            
            // We delay disabling rendering as animations for showing the modal
            // may still be playing
            if (hideFontMap)
            {
                await Task.Delay(200);
                if (AreModalsOpen())
                    FontMap.Visibility = Visibility.Collapsed;
            }

            UpdateModalStates(AreModalsOpen());
        }

        void OnModalClosed()
        {
            if (AreModalsOpen())
            {
                FontMap.Visibility = Visibility.Collapsed;
                UpdateModalStates(true);
            }
            else
            {
                FontMap.Visibility = Visibility.Visible;
                UpdateModalStates(false);
            }
        }

        void UpdateModalStates(bool open)
        {
            string state = open ? nameof(ModalOpenState) : nameof(NoModalOpenState);
            VisualStateManager.GoToState(this, state, true);
        }


        private bool AreModalsOpen()
        {
            return (SettingsView != null && SettingsView.IsOpen)
                     || (PrintPresenter != null && PrintPresenter.Child != null);
        }

        private void LayoutRoot_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.F11)
                Utils.ToggleFullScreenMode();

            // If ALT key is held down, ignore
            if (e.KeyStatus.IsMenuKeyDown)
                return;

            var ctrlState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Control);
            if ((ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
            {
                // Check to see if any basic modals are open first
                if (AreModalsOpen())
                    return;

                if (!FontMap.HandleInput(e))
                {
                    switch (e.Key)
                    {
                        case VirtualKey.N:
                            if (ViewModel.SelectedFont is InstalledFont fnt)
                                _ = FontMapView.CreateNewViewForFontAsync(fnt);
                            break;
                        case VirtualKey.Delete:
                            if (ViewModel.SelectedFont is InstalledFont font && font.HasImportedFiles)
                                FlyoutHelper.RequestDelete(font);
                            break;
                        case VirtualKey.L:
                            TogglePane_Click(null, null);
                            break;
                    }
                }
            }
        }

        private void FontsTabBar_Loaded(object sender, RoutedEventArgs e)
        {
            // 1. Create the popup Menu
            if (sender is FrameworkElement f
                && f.GetDescendantsOfType<Button>().FirstOrDefault(b => b.Name == "CollectionButton") is Button b
                && b.Flyout is MenuFlyout menu
                && menu.Items.FirstOrDefault() is MenuFlyoutItem item)
            {
                item.Click -= Item_Click;
                item.Click += Item_Click;

                menu.Opening -= Menu_Opening;
                menu.Opening += Menu_Opening;
            }

            List<InstalledFont> GetActiveFonts()
            {
                return ViewModel.Fonts.Where(f => !f.IsCompact).Select(f => f.Font).Distinct().ToList();
            }

            void Menu_Opening(object sender, object e)
            {
                // Build Menu
                if (sender is MenuFlyout menu)
                {
                    if (ViewModel.Fonts.All(f => f.IsCompact))
                    {
                        // If all tabs are collapsed there are no active fonts so these 
                        // buttons no nothing
                        foreach (var item in menu.Items)
                            item.IsEnabled = false;
                    }
                    else
                    {
                        foreach (var item in menu.Items)
                            item.IsEnabled = true;

                        if (menu.Items.OfType<MenuFlyoutSubItem>().FirstOrDefault() is MenuFlyoutSubItem c)
                        {
                            // Rebuild the collections menu
                            menu.Items.Remove(c);
                            FlyoutHelper.AddCollectionItems(menu, null, GetActiveFonts(), "AddActiveToCollectionItem/Text");
                        }
                    }
                }
            }

            void Item_Click(object sender, RoutedEventArgs e)
            {
                _ = QuickCompareView.CreateWindowAsync(
                        new(false, new(GetActiveFonts())));
            }
        }

        private void LstFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.IsLoadingFonts is false && 
                e.AddedItems.FirstOrDefault() is InstalledFont font)
            {
                ViewModel.SelectedFont = font;
            }
        }

        private void OpenFontPaneButton_Click(object sender, RoutedEventArgs e)
        {
            SplitView.IsPaneOpen = !SplitView.IsPaneOpen;
        }

        void OnCollectionsUpdated(CollectionsUpdatedMessage msg)
        {
            if (ViewModel.InitialLoad.IsCompleted)
            {
                if (Dispatcher.HasThreadAccess)
                    ViewModel.RefreshFontList(ViewModel.SelectedCollection);
                else
                    RunOnUI(() => ViewModel.RefreshFontList(ViewModel.SelectedCollection));
            }
        }

        private string UpdateFontCountLabel(List<InstalledFont> fontList, bool keepCasing)
        {
            if (fontList != null)
            {
                string s = (string)ResourceHelper.Get<IValueConverter>("TitleConverter").Convert(
                    Localization.Get("StatusBarFontCount", fontList.Count), typeof(string), null, null);

                // Hack for Zune Theme.
                if (!keepCasing)
                    s = s.ToUpper();

                return s;
            }

            return string.Empty;
        }

        private void OnFilterClick(object parameter)
        {
            if (parameter is BasicFontFilter filter)
            {
                if (!FontsSemanticZoom.IsZoomedInViewActive)
                    FontsSemanticZoom.IsZoomedInViewActive = true;

                if (filter == ViewModel.FontListFilter)
                    ViewModel.RefreshFontList();
                else
                    ViewModel.FontListFilter = filter;
            }
        }

        private void OnFontPreviewUpdated()
        {
            if (ViewModel.InitialLoad.IsCompleted)
            {
                _fontListDebouncer.Debounce(16, () =>
                {
                    RunOnUI(() =>
                    {
                        // Need the fonts to update the fonts used to show the names
                        // in the font list which are only evaluated on creating the 
                        // ItemTemplate, so make a new list;
                        ViewModel.RefreshFontList(ViewModel.SelectedCollection);

                        // Update tabs font previews
                        ViewModel.NotifyTabs();
                    });
                });
            }
        }

        private void FontCompareButton_Click(object sender, RoutedEventArgs e)
        {
            _ = QuickCompareView.CreateWindowAsync(new(false, ViewModel.Folder));
        }

        private void LstFontFamily_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            args.ItemContainer.PointerPressed -= ItemContainer_PointerPressed;
            args.ItemContainer.PointerPressed += ItemContainer_PointerPressed;

            args.ItemContainer.ContextRequested -= ItemContainer_ContextRequested;
            args.ItemContainer.ContextRequested += ItemContainer_ContextRequested;
        }

        private void ItemContainer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            /* MIDDLE CLICK FOR FONT LIST */
            var pointer = e.GetCurrentPoint(sender as FrameworkElement);
            if (pointer.Properties.IsMiddleButtonPressed
                && sender is ListViewItem f
                && f.Content is InstalledFont font)
            {
                if (ViewModel.Settings.DisableTabs 
                    || ResourceHelper.SupportsTabs is false
                    || Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
                    _ = FontMapView.CreateNewViewForFontAsync(font);
                else
                {
                    ViewModel.Fonts.Insert(ViewModel.TabIndex + 1, new(font));

                    if (Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
                        ViewModel.TabIndex += 1;
                }
            }
        }

        private void ItemContainer_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            /* RIGHT CLICK MENU FOR FONT LIST */
            if (sender is ListViewItem f && f.Content is InstalledFont font)
            {
                args.Handled = true;
                FlyoutBase.SetAttachedFlyout(f, FontListFlyout);
                FlyoutHelper.CreateMenu(
                        FontListFlyout,
                        font,
                        CharacterRenderingOptions.CreateDefault(font),
                        null,
                        new () { 
                            Folder = ViewModel.Folder, 
                            PreviewText = FontMap.ViewModel.Sequence,
                        });

                args.TryGetPosition(sender, out Point pos);
                FontListFlyout.ShowAt(sender, pos);
            }
        }

        private void OpenFolder()
        {
            _ = (new OpenFolderDialog()).ShowAsync();
        }




        /* Font Collection management */

        private void RenameFontCollection_Click(object sender, RoutedEventArgs e)
        {
            _ = (new CreateCollectionDialog(ViewModel.SelectedCollection)).ShowAsync();
        }

        private void DeleteCollection_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog d = new ()
            {
                Title = Localization.Get("DigDeleteCollection/Title"),
                IsPrimaryButtonEnabled = true,
                IsSecondaryButtonEnabled = true,
                PrimaryButtonText = Localization.Get("DigDeleteCollection/PrimaryButtonText"),
                SecondaryButtonText = Localization.Get("DigDeleteCollection/SecondaryButtonText"),
            };

            d.PrimaryButtonClick += DigDeleteCollection_PrimaryButtonClick;
            _ = d.ShowAsync();
        }

        private async void DigDeleteCollection_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            string name = ViewModel.SelectedCollection.Name;
            await ViewModel.FontCollections.DeleteCollectionAsync(ViewModel.SelectedCollection);
            ViewModel.RefreshFontList();

            Messenger.Send(new AppNotificationMessage(true, $"\"{name}\" collection deleted"));
        }




        /* Drag / Drop support */

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
                e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                ViewModel.IsLoadingFonts = true;
                try
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    if (await FontFinder.ImportFontsAsync(items) is FontImportResult result)
                    {
                        if (result.Imported.Count > 0)
                        {
                            ViewModel.RefreshFontList();
                            ViewModel.RestoreOpenFonts();
                            ViewModel.TrySetSelectionFromImport(result);
                        }

                        ShowImportResult(result);
                    }
                }
                finally
                {
                    ViewModel.IsLoadingFonts = false;
                }
            }
        }

        private async void OpenFont()
        {
            var picker = new FileOpenPicker();
            foreach (var format in FontFinder.ImportFormats)
                picker.FileTypeFilter.Add(format);

            picker.CommitButtonText = Localization.Get("OpenFontPickerConfirm");
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    ViewModel.IsLoadingFonts = true;

                    if (file.FileType == ".zip")
                    {
                        if (await FontFinder.LoadZipToTempFolderAsync(file) is FolderContents folder && folder.Fonts.Count > 0)
                        {
                            await MainPage.CreateWindowAsync(new(
                                Ioc.Default.GetService<IDialogService>(),
                                Ioc.Default.GetService<AppSettings>(),
                                folder));
                        }
                    }
                    else if (await FontFinder.LoadFromFileAsync(file) is InstalledFont font)
                    {
                        await FontMapView.CreateNewViewForFontAsync(font, file);
                    }
                }
                finally
                {
                    ViewModel.IsLoadingFonts = false;
                }
            }
        }

        private void OnFontImportRequest(ImportMessage msg)
        {
            _ = CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                ViewModel.IsLoadingFonts = true;
                try
                {
                    if (ViewModel.InitialLoad.IsCompleted)
                    {
                        ViewModel.RefreshFontList();
                    }
                    else
                    {
                        await ViewModel.InitialLoad;
                        await Task.Delay(50);
                    }

                    ViewModel.TrySetSelectionFromImport(msg.Result);
                }
                finally
                {
                    ViewModel.IsLoadingFonts = false;
                    ShowImportResult(msg.Result);
                }
            });
        }

        void ShowImportResult(FontImportResult result)
        {
            if (result.Imported.Count == 1)
                InAppNotificationHelper.ShowNotification(this, Localization.Get("NotificationSingleFontAdded", result.Imported[0].Name), 4000);
            else if (result.Imported.Count > 1)
                InAppNotificationHelper.ShowNotification(this, Localization.Get("NotificationMultipleFontsAdded", result.Imported.Count), 4000);
            else if (result.Invalid.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(Localization.Get("NotificationImportFailed"));
                sb.AppendLine();
                foreach (var i in result.Invalid.Take(5))
                {
                    sb.AppendLine($"{i.Item1.Name}: {i.Item2}");
                }

                if (result.Invalid.Count > 5)
                    sb.Append("…");

                InAppNotificationHelper.ShowNotification(this, sb.ToString().Trim(), 4000);
            }
        }




        /* Printing */

        public Border GetPresenter()
        {
            this.FindName(nameof(PrintPresenter));
            return PrintPresenter;
        }

        public FontMapView GetFontMap() => FontMap;

        public GridLength GetTitleBarHeight() => TitleBar.TemplateSettings.GridHeight;




        /* Notifications */

        public InAppNotification GetNotifier()
        {
            if (NotificationRoot == null)
                this.FindName(nameof(NotificationRoot));

            return DefaultNotification;
        }

        void OnNotificationMessage(AppNotificationMessage msg)
        {
            InAppNotificationHelper.OnMessage(this, msg);
            
            if (msg.Data is AddToCollectionResult result 
                && result.Success 
                && result.Collection is not null
                && result.Collection == ViewModel.SelectedCollection)
            {
                // If we don't have thread access, it means another window has added an item to
                // the collection we're currently viewing, and we should refresh our view

                RunOnUI(() => ViewModel.RefreshFontList(ViewModel.SelectedCollection));
            }
        }




        /* Composition */

        private void Grid_Loading(FrameworkElement sender, object args)
        {
            CompositionFactory.SetThemeShadow(sender, 40, PaneRoot);
        }

        private void FontListGrid_Loading(FrameworkElement sender, object args)
        {
            CompositionFactory.SetDropInOut(
                CollectionControlBackground,
                CollectionControlItems.Children.Cast<FrameworkElement>().ToList(),
                CollectionControlRow);
        }

        private void LoadingRoot_Loading(FrameworkElement sender, object args)
        {
            if (!ViewModel.Settings.UseSelectionAnimations 
                || !CompositionFactory.UISettings.AnimationsEnabled)
                return;

            var v = sender.GetElementVisual();

            CompositionFactory.StartCentering(v);

            int duration = 350;
            var ani = v.Compositor.CreateVector3KeyFrameAnimation();
            ani.Target = nameof(v.Scale);
            ani.InsertKeyFrame(1, new System.Numerics.Vector3(1.15f, 1.15f, 0));
            ani.Duration = TimeSpan.FromMilliseconds(duration);

            var op = CompositionFactory.CreateFade(v.Compositor, 0, null, duration);
            sender.SetHideAnimation(v.Compositor.CreateAnimationGroup(ani, op));

            // Animate in Loading items
            CompositionFactory.PlayEntrance(LoadingStack.Children.ToList(), 60);
        }
    }

    public partial class MainPage
    {
        public static async Task<WindowInformation> CreateWindowAsync(MainViewModelArgs args)
        {
            static void CreateView(MainViewModelArgs a)
            {
                MainPage view = new(a);
                Window.Current.Content = view;
                Window.Current.Activate();
            }

            var view = await WindowService.CreateViewAsync(() => CreateView(args), false);
            await WindowService.TrySwitchToWindowAsync(view, false);
            return view;
        }
    }
}
