using CharacterMap.Annotations;
using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace CharacterMap.Views
{
    public sealed partial class SettingsView : ViewBase
    {
        private Random _random { get; } = new Random();

        public AppSettings Settings { get; }
        public UserCollectionsService FontCollections { get; }
        public List<SupportedLanguage> SupportedLanguages { get; }

        public SettingsView()
        {
            Settings = ResourceHelper.AppSettings;
            FontCollections = SimpleIoc.Default.GetInstance<UserCollectionsService>();
            Messenger.Default.Register<AppSettingsChangedMessage>(this, OnAppSettingsUpdated);

            this.InitializeComponent();
            SetupAnimations();

            RbLanguage.ItemsSource = new List<String> { "XAML", "C#" };
            RbLanguage.SelectedIndex = Settings.DevToolsLanguage;

            SupportedLanguages = new List<SupportedLanguage>(
                ApplicationLanguages.ManifestLanguages.
                Select(language => new SupportedLanguage(language)));
            SupportedLanguages.Insert(0, SupportedLanguage.SystemLanguage);
        }

        void OnAppSettingsUpdated(AppSettingsChangedMessage msg)
        {
            if (msg.PropertyName == nameof(Settings.UserRequestedTheme))
                OnPropertyChanged(nameof(Settings));
        }

        private void SetupAnimations()
        {
            Visual v = this.EnableTranslation(true).GetElementVisual();

            var o = v.Compositor.CreateScalarKeyFrameAnimation();
            o.Target = nameof(Visual.Opacity);
            o.InsertKeyFrame(1, 0);
            o.Duration = TimeSpan.FromSeconds(0.2);

            var t = v.Compositor.CreateVector3KeyFrameAnimation();
            t.Target = Composition.TRANSLATION;
            t.InsertKeyFrame(1, new Vector3(0, 200, 0));
            t.Duration = TimeSpan.FromSeconds(0.375);

            this.SetHideAnimation(v.Compositor.CreateAnimationGroup(t, o));

            this.SetShowAnimation(Composition.CreateEntranceAnimation(this, new Vector3(0, 200, 0), 0, 550));
            LeftPanel.SetShowAnimation(Composition.CreateEntranceAnimation(LeftPanel, new Vector3(0, 140, 0), Composition.DEFAULT_STAGGER_MS, 850));
            RightPanel.SetShowAnimation(Composition.CreateEntranceAnimation(RightPanel, new Vector3(0, 140, 0), Composition.DEFAULT_STAGGER_MS * 2, 850));
        }

        public void Show(FontVariant variant, InstalledFont font)
        {
            this.Visibility = Visibility.Visible;
            
            // 1. Focus the close button to ensure keyboard focus is retained inside the settings panel
            BtnClose.Focus(FocusState.Programmatic);

#pragma warning disable CS0618 // ChangeView doesn't work well when not properly visible
            ContentScroller.ScrollToVerticalOffset(0);
#pragma warning restore CS0618




            // 2. Get the fonts used for Font List  & Character Grid previews
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
            RbLanguage.SelectedIndex = Settings.DevToolsLanguage;
        }

        public void Hide()
        {
            this.Visibility = Visibility.Collapsed;
        }

        private void View_Loading(FrameworkElement sender, object args)
        {
            Composition.SetThemeShadow(HeaderGrid, 40, ContentScroller);

            // Set the settings that can't be set with bindings

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

            if (Settings.UseFontForPreview)
                UseActualFont.IsChecked = true;
            else
                UseSystemFont.IsChecked = true;

        }

        private void BtnReview_Click(object sender, RoutedEventArgs e)
        {
            _ = Microsoft.Toolkit.Uwp.Helpers.SystemInformation.LaunchStoreForReviewAsync();
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

        private void RadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.DevToolsLanguage = ((RadioButtons)sender).SelectedIndex;
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
    }
}
