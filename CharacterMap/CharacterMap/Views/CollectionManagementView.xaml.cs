using CharacterMap.Controls;
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

namespace CharacterMap.Views
{
    public interface IActivateableControl
    {
        void Activate();
        void Deactivate();

    }

    public sealed partial class CollectionManagementView : UserControl, IActivateableControl
    {
        CollectionManagementViewModel ViewModel { get; }
        
        public CollectionManagementView()
        {
            this.InitializeComponent();
            ViewModel = new();
        }

        public void Activate()
        {
            ViewModel.Activate();
        }

        public void Deactivate()
        {
            ViewModel.Deactivate();
        }

        private void CollectionSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.RefreshFontLists();
        }

        private async void RenameFontCollection_Click(object sender, RoutedEventArgs e)
        {
            await (new CreateCollectionDialog(ViewModel.SelectedCollection)).ShowAsync();
            this.Bindings.Update();
        }

        private void DeleteCollection_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
