using CharacterMap.Core;
using CharacterMap.Models;
using CharacterMap.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace CharacterMap.Controls
{
    public sealed partial class FontMapPrintPage : Page
    {
        PrintViewModel PrintModel { get; }



        public bool IsInAppPreview
        {
            get { return (bool)GetValue(IsInAppPreviewProperty); }
            set { SetValue(IsInAppPreviewProperty, value); }
        }

        public static readonly DependencyProperty IsInAppPreviewProperty =
            DependencyProperty.Register(nameof(IsInAppPreview), typeof(bool), typeof(FontMapPrintPage), new PropertyMetadata(false));



        public FontMapPrintPage(FontMapViewModel viewModel, PrintViewModel printModel, DataTemplate t, bool isAppPreview = false)
        {
            PrintModel = printModel;

            this.InitializeComponent();

            IsInAppPreview = isAppPreview;

            Update();
            ItemsPanel.EnableResizeAnimation = false;
            ItemsPanel.ItemTemplate = t;
            ItemsPanel.ItemFontFace = viewModel.SelectedVariant.FontFace;
            ItemsPanel.ItemFontFamily = viewModel.FontFamily;
            ItemsPanel.ItemTypography = viewModel.SelectedTypography;
            ItemsPanel.ShowColorGlyphs = viewModel.ShowColorGlyphs;
            ItemsPanel.ShowUnicodeDescription = viewModel.Settings.ShowCharGridUnicode;
        }

        public void Update()
        {
            ItemsPanel.UpdateSize(PrintModel.GlyphSize);
        }

        public static int CalculateGlyphsPerPage(Size printSize, PrintViewModel viewModel)
        {
            double size = viewModel.GlyphSize + 4d + 4d; // 4px is GridViewItem padding, 4px is border-thickness.

            var c = (int)Math.Floor((printSize.Width + 6) / size);
            var r = (int)Math.Floor((printSize.Height) / size);

            return r * c; 
        }

        public bool AddCharacters(int page, int charsPerPage, IReadOnlyCollection<Character> e)
        {
            foreach (var c in e.Skip((page) * charsPerPage).Take(charsPerPage))
                ItemsPanel.Items.Add(c);

            // Are there still more characters in the font too add?
            return e.Count > (page + 1) * charsPerPage;
        }

        public void ClearCharacters()
        {
            ItemsPanel.Items.Clear();
        }


        private Thickness GetMargin(double horizontal, double vertical)
        {
            return new Thickness(horizontal, vertical, horizontal, vertical);
        }
    }
}
