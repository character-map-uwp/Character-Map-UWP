using CharacterMap.Helpers;
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
    public sealed partial class CategoryFlyout : Flyout
    {
        public event EventHandler<IList<UnicodeCategoryModel>> AcceptClicked;

        public object SourceCategories
        {
            get { return (object)GetValue(SourceCategoriesProperty); }
            set { SetValue(SourceCategoriesProperty, value); }
        }

        public static readonly DependencyProperty SourceCategoriesProperty =
            DependencyProperty.Register(nameof(SourceCategories), typeof(object), typeof(CategoryFlyout), new PropertyMetadata(null));

        public CategoryFlyout()
        {
            this.InitializeComponent();
        }

        private void Flyout_Opening(object sender, object e)
        {
            CategoryList.ItemsSource = Unicode.CreateCategoriesList(SourceCategories as List<UnicodeCategoryModel>);
        }

        private void FilterAccept_Click(object sender, RoutedEventArgs e)
        {
            AcceptClicked?.Invoke(this, (List<UnicodeCategoryModel>)CategoryList.ItemsSource);
            this.Hide();
        }

        private void FilterRefresh_Click(object sender, RoutedEventArgs e)
        {
            CategoryList.ItemsSource = Unicode.CreateCategoriesList(SourceCategories as List<UnicodeCategoryModel>);
        }

        private void FilterSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in ((List<UnicodeCategoryModel>)CategoryList.ItemsSource))
                item.IsSelected = true;
        }

        private void FilterClear_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in ((List<UnicodeCategoryModel>)CategoryList.ItemsSource))
                item.IsSelected = false;
        }
    }
}
