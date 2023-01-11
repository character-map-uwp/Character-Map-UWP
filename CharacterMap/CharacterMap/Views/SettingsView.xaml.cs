using CharacterMap.Controls;
using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.ApplicationModel.Core;
using Windows.Globalization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Views
{
    public class ChangelogItem
    {
        public ChangelogItem(string header, string content)
        {
            Header = header;
            Content = content;
        }

        public string Header { get; set; }
        public string Content { get; set; }
    }

    public sealed partial class SettingsView : ViewBase
    {
        private Random _random { get; } = new Random();

        public AppSettings Settings { get; }

        public UserCollectionsService FontCollections { get; }

        public SettingsViewModel ViewModel { get; }

        public bool IsOpen { get; private set; }

        [ObservableProperty] 
        private GridLength _titleBarHeight = new GridLength(32);

        public int GridSize
        {
            get { return (int)GetValue(GridSizeProperty); }
            set { SetValue(GridSizeProperty, value); }
        }

        public static readonly DependencyProperty GridSizeProperty =
            DependencyProperty.Register(nameof(GridSize), typeof(int), typeof(SettingsView), new PropertyMetadata(0d));

        private bool _themeSupportsShadows = false;
        private bool _themeSupportsDark = false;
        private NavigationHelper _navHelper { get; } = new NavigationHelper();

        public SettingsView()
        {
            this.InitializeComponent();

            if (DesignMode)
                return;

            ViewModel = new();
            Settings = ResourceHelper.AppSettings;
            FontCollections = Ioc.Default.GetService<UserCollectionsService>();
            Register<AppSettingsChangedMessage>(OnAppSettingsUpdated);
            Register<FontListCreatedMessage>(m => UpdateExport());

            GridSize = Settings.GridSize;
            FontNamingSelection.SelectedIndex = (int)Settings.ExportNamingScheme;

            _themeSupportsShadows = ResourceHelper.SupportsShadows();
            _themeSupportsDark = ResourceHelper.Get<Boolean>("SupportsDarkTheme");

            _navHelper.BackRequested += (s, e) => Hide();
        }

        void OnAppSettingsUpdated(AppSettingsChangedMessage msg)
        {
            if (!Dispatcher.HasThreadAccess)
            {
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    OnAppSettingsUpdated(msg);
                });
                return;
            }
            switch (msg.PropertyName)
            {
                case nameof(Settings.UserRequestedTheme):
                    OnPropertyChanged(nameof(Settings));
                    break;
                case nameof(Settings.GridSize):
                    // We can't direct bind here as it may be updated from a different UI Thread.
                    GridSize = Settings.GridSize;
                    break;
                case nameof(Settings.ApplicationDesignTheme):
                    UpdateStyle();
                    break;
            }
        }

        int _requested = 0;

        public void Show(FontVariant variant, InstalledFont font, int idx = 0)
        {
            if (IsOpen)
                return;

            UpdateAnimation();

            StartShowAnimation();
            this.Visibility = Visibility.Visible;

            if (!ResourceHelper.AllowAnimation)
            {
                this.GetElementVisual().Opacity = 1;
                this.GetElementVisual().Properties.InsertVector3(CompositionFactory.TRANSLATION, Vector3.Zero);
            }

            // 1. Focus the close button to ensure keyboard focus is retained inside the settings panel
            Presenter.SetDefaultFocus();

#pragma warning disable CS0618 // ChangeView doesn't work well when not properly visible
            ContentScroller.ScrollToVerticalOffset(0);
#pragma warning restore CS0618

            // 2. Get the fonts used for Font List & Character Grid previews
            // Note: it is legal for both "variant" and "font" to be NULL
            //       when calling, so test both cases.
            bool isSymbol = FontCollections.IsSymbolFont(font);

            Preview1.FontFamily = Preview2.FontFamily = Preview3.FontFamily 
                = variant != null && !isSymbol ? new FontFamily(variant.XamlFontSource) : FontFamily.XamlAutoFontFamily;

            var items = Enumerable.Range(1, 5).Select(i => FontFinder.Fonts[_random.Next(0, FontFinder.Fonts.Count - 1)])
                                              .OrderBy(f => f.Name)
                                              .ToList();

            if (font != null && !isSymbol && !items.Contains(font))
            {
                items.RemoveAt(0);
                items.Add(font);
            }

            LstFontFamily.ItemsSource =  items.OrderBy(f => f.Name).ToList();
            
            // 3. Set correct Developer features language
            UpdateExport();

            Presenter.SetTitleBar();
            IsOpen = true;

            _navHelper.Activate();

            _requested = idx;
            if (IsLoaded)
                MenuColumn.Children.OfType<MenuButton>().ElementAt(idx).IsChecked = true;
        }

        public void Hide()
        {
            UpdateAnimation();
            _navHelper.Deactivate();

            TitleBarHelper.RestoreDefaultTitleBar();
            IsOpen = false;
            this.Visibility = Visibility.Collapsed;
            Messenger.Send(new ModalClosedMessage());
        }

        private void UpdateAnimation()
        {
            if (ResourceHelper.AllowAnimation)
                CompositionFactory.SetupOverlayPanelAnimation(this);
            else
            {
                this.SetShowAnimation(null);
                this.SetHideAnimation(null);
            }
        }

        private void StartShowAnimation()
        {
            if (!ResourceHelper.AllowAnimation)
                return;

            List<UIElement> elements = new() { this, MenuColumn, ContentBorder };
            CompositionFactory.PlayEntrance(elements, 0, 200);
            UpdateStyle();
        }

        protected override void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Override base so we do not unregister messages
        }

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateStyle();

            ((RadioButton)MenuColumn.Children.ElementAt(_requested)).IsChecked = true;

            // Set the settings that can't be set with bindings
            if (_themeSupportsDark && ThemeSystem is not null)
            {
                switch (Settings.UserRequestedTheme)
                {
                    case ElementTheme.Default:
                        ThemeSystem.IsChecked = true;
                        break;
                    case ElementTheme.Light:
                        ThemeLight.IsChecked = true;
                        break;
                    case ElementTheme.Dark:
                        ThemeDark.IsChecked = true;
                        break;
                }
            }

            if (Settings.UseFontForPreview)
                UseActualFont.IsChecked = true;
            else
                UseSystemFont.IsChecked = true;
        }


        /* Work around to avoid binding threading issues */
        private int GetGridSize(int s) => s;

        private void UpdateGridSize(double d)
        {
            GridSize = Settings.GridSize = (int)d;
        }

        private void UpdateExport()
        {
            this.RunOnUI(() =>
            {
                ImportedExportPanel.SetVisible(FontFinder.ImportedFonts.Count > 0);
            });
        }

        private void BtnReview_Click(object sender, RoutedEventArgs e)
        {
            _ = SystemInformation.LaunchStoreForReviewAsync();
        }

        private void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            _ = CoreApplication.RequestRestartAsync(string.Empty);
        }

        private void ThemeLight_Checked(object sender, RoutedEventArgs e)
        {
            Settings.UserRequestedTheme = ElementTheme.Light;
        }

        private void ThemeDark_Checked(object sender, RoutedEventArgs e)
        {
            Settings.UserRequestedTheme = ElementTheme.Dark;
        }

        private void ThemeSystem_Checked(object sender, RoutedEventArgs e)
        {
            Settings.UserRequestedTheme = ElementTheme.Default;
        }

        private void FontNamingSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.ExportNamingScheme = (ExportNamingScheme)((RadioButtons)sender).SelectedIndex;
        }

        private void UseSystemFont_Checked(object sender, RoutedEventArgs e)
        {
            Settings.UseFontForPreview = false;
            ResetFontPreview();
        }

        private void UseActualFont_Checked(object sender, RoutedEventArgs e)
        {
            Settings.UseFontForPreview = true;
            ResetFontPreview();
        }

        private void ResetFontPreview()
        {
            var items = LstFontFamily.ItemsSource;
            LstFontFamily.ItemsSource = null;
            LstFontFamily.ItemsSource = items;
        }

        public void SelectedLanguageToString(object selected) => 
            Settings.AppLanguage = selected is SupportedLanguage s ? s.LanguageID : "en-US";


        private void MenuItem_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton item
              && item.Tag is Panel panel
              && panel.Visibility == Visibility.Collapsed)
            {
                // 1: Ensure all settings panels are hidden
                foreach (var child in ContentPanel.Children.OfType<FrameworkElement>())
                {
                    // 2: Deactivate old content if supported
                    if (child.Visibility == Visibility.Visible
                        && child is Panel p
                        && p.Children.Count == 1
                        && p.Children[0] is IActivateableControl d)
                        d.Deactivate();

                    child.Visibility = Visibility.Collapsed;
                }

                // 3: Reset scroll position
                ContentScroller.ChangeView(null, 0, null, true);

                // 4: Activate new content if supported
                if (panel.Children.Count == 1 && panel.Children[0] is IActivateableControl a)
                {
                    a.Activate();
                    VisualStateManager.GoToState(this, ContentScrollDisabledState.Name, false);
                }
                else
                    VisualStateManager.GoToState(this, ContentScrollEnabledState.Name, false);


                // 5: Start child animation
                if (Settings.UseSelectionAnimations)
                    CompositionFactory.PlayEntrance(GetChildren(panel), 0, 80);

                // 6: Show selected panel
                panel.Visibility = Visibility.Visible;
            }
        }

        private List<UIElement> GetChildren(Panel p)
        {
            List<UIElement> children = new List<UIElement>();

            foreach (var child in p.Children)
            {
                if (child is ItemsControl c)
                {
                    c.Realize(p.DesiredSize.Width, p.DesiredSize.Height);
                    children.AddRange(c.ItemsPanelRoot.Children);
                }
                else
                    children.Add(child);
            }

            return children;
        }


        void UpdateStyle()
        {
            //string key = Settings.ApplicationDesignTheme == 0 ? "Default" : "FUI";
            //if (Settings.ApplicationDesignTheme == 2) key = "Zune";
            //var controls = this.GetDescendants().OfType<FrameworkElement>().Where(e => e is not IThemeableControl && Properties.GetStyleKey(e) is not null).ToList();
            //foreach (var p in controls)
            //{
            //    string target = $"{key}{Properties.GetStyleKey(p)}";
            //    Style style = ResourceHelper.Get<Style>(this, target);
            //    p.Style = style;
            //}

            //ResourceHelper.SendThemeChanged();

            string key = Settings.ApplicationDesignTheme switch
            {
                1 => "FUI",
                2 => "Zune",
                _ => "Default"
            };
            bool t = GoToState($"{key}ThemeState");
        }



        /* CONVERTERS */

        Visibility ShowUnicode(GlyphAnnotation annotation)
        {
            return annotation != GlyphAnnotation.None ? Visibility.Visible : Visibility.Collapsed;
        }


        private void Design_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.ApplicationDesignTheme = ((ComboBox)sender).SelectedIndex;
        }
    }
}
