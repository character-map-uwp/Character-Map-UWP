using CharacterMap.Core;
using CharacterMap.ViewModels;
using CommonServiceLocator;
using GalaSoft.MvvmLight.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace CharacterMap.Views
{
    public sealed partial class FontMapView : UserControl
    {
        public InstalledFont Font
        {
            get { return (InstalledFont)GetValue(FontProperty); }
            set { SetValue(FontProperty, value); }
        }

        public static readonly DependencyProperty FontProperty =
            DependencyProperty.Register(nameof(Font), typeof(InstalledFont), typeof(FontMapView), new PropertyMetadata(null, (d,e) =>
            {
                ((FontMapView)d).ViewModel.SelectedFont = e.NewValue as InstalledFont;
            }));

        public bool IsStandalone
        {
            get { return (bool)GetValue(IsStandaloneProperty); }
            set { SetValue(IsStandaloneProperty, value); }
        }

        public static readonly DependencyProperty IsStandaloneProperty =
            DependencyProperty.Register(nameof(IsStandalone), typeof(bool), typeof(FontMapView), new PropertyMetadata(false));



        public FontMapViewModel ViewModel
        {
            get { return (FontMapViewModel)GetValue(ViewModelProperty); }
            private set { SetValue(ViewModelProperty, value); }
        }

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(FontMapViewModel), typeof(FontMapView), new PropertyMetadata(null));


        public AppSettings Settings { get; }

        public FontMapView()
        {
            this.InitializeComponent();
            Settings = (AppSettings)App.Current.Resources[nameof(AppSettings)];
            ViewModel = new FontMapViewModel(ServiceLocator.Current.GetInstance<IDialogService>());
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

        public void TryScrollSelectionIntoView()
        {
            if (null != CharGrid.SelectedItem)
            {
                CharGrid.ScrollIntoView(CharGrid.SelectedItem, ScrollIntoViewAlignment.Leading);
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

        
    }
}
