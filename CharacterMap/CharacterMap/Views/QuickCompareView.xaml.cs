using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace CharacterMap.Views
{
    public sealed partial class QuickCompareView : ViewBase
    {
        public QuickCompareViewModel ViewModel { get; }

        public QuickCompareView()
        {
            this.InitializeComponent();
            ViewModel = new QuickCompareViewModel();
        }

        public static async Task CreateNewWindowAsync()
        {
            static void CreateView()
            {
                QuickCompareView view = new QuickCompareView();
                Window.Current.Content = view;
                Window.Current.Activate();
            }

            var view = await WindowService.CreateViewAsync(CreateView, false);
            await WindowService.TrySwitchToWindowAsync(view, false);
        }

        private void MenuFlyout_Opening(object sender, object e)
        {
            // Handles forming the flyout when opening the main FontFilter 
            // drop down menu.
            if (sender is MenuFlyout menu)
            {
                // Reset to default menu
                while (menu.Items.Count > 8)
                    menu.Items.RemoveAt(8);

                // force menu width to match the source button
                foreach (var sep in menu.Items.OfType<MenuFlyoutSeparator>())
                    sep.MinWidth = FontListFilter.ActualWidth;

                // add users collections 
                if (ViewModel.FontCollections.Items.Count > 0)
                {
                    menu.Items.Add(new MenuFlyoutSeparator());
                    foreach (var item in ViewModel.FontCollections.Items)
                    {
                        var m = new MenuFlyoutItem { DataContext = item, Text = item.Name, FontSize = 16 };
                        m.Click += (s, a) =>
                        {
                            if (m.DataContext is UserFontCollection u)
                            {

                                ViewModel.SelectedCollection = u;
                            }
                        };
                        menu.Items.Add(m);
                    }
                }

                VariableOption.SetVisible(FontFinder.HasVariableFonts);

                if (!FontFinder.HasAppxFonts && !FontFinder.HasRemoteFonts)
                {
                    FontSourceSeperator.Visibility = CloudFontsOption.Visibility = AppxOption.Visibility = Visibility.Collapsed;
                }
                else
                {
                    FontSourceSeperator.Visibility = Visibility.Visible;
                    CloudFontsOption.SetVisible(FontFinder.HasRemoteFonts);
                    AppxOption.SetVisible(FontFinder.HasAppxFonts);
                }

                static void SetCommand(MenuFlyoutItemBase b, ICommand c)
                {
                    b.FontSize = 16;
                    if (b is MenuFlyoutSubItem i)
                    {
                        foreach (var child in i.Items)
                            SetCommand(child, c);
                    }
                    else if (b is MenuFlyoutItem m)
                        m.Command = c;
                }

                foreach (var item in menu.Items)
                    SetCommand(item, ViewModel.FilterCommand);
            }
        }

        private void Repeater_EffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            Debug.WriteLine($"{args.EffectiveViewport}");
        }




        /*
         * ElementName Bindings don't work inside ItemsRepeater, so to change
         * preview Text & FontSize we need to manually update all TextBlocks
         */

        private void InputText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Repeater == null)
                return;

            XamlBindingHelper.SuspendRendering(Repeater);

            string text = ((TextBox)sender).Text;
            foreach (var g in Repeater.GetFirstLevelDescendantsOfType<Grid>().Where(g => g.ActualOffset.X >= 0))
            {
                SetText(g, text);
            }

            XamlBindingHelper.ResumeRendering(Repeater);
        }

        private void FontSizeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (Repeater == null)
                return;

            XamlBindingHelper.SuspendRendering(Repeater);
            double v = e.NewValue;
            foreach (var g in Repeater.GetFirstLevelDescendantsOfType<Grid>().Where(g => g.ActualOffset.X >= 0))
            {
                SetFontSize(g, v);
            }
            XamlBindingHelper.ResumeRendering(Repeater);
        }

        void SetText(Grid root, string text)
        {
            ((TextBlock)root.Children[1]).Text = text;
        }

        void SetFontSize(Grid root, double size)
        {
            ((TextBlock)root.Children[1]).FontSize = size;
        }

        private void Repeater_ElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            if (args.Element is Button b && b.Content is Grid g)
            {
                SetText(g, InputText.Text);
                SetFontSize(g, FontSizeSlider.Value);
            }
        }
    }
}
