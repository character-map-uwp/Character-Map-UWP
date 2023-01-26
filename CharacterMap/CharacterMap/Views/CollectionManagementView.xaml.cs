using CharacterMap.Controls;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
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

        private async void NewCollection_Click(object sender, RoutedEventArgs e)
        {
            CreateCollectionDialog d = new();
            await d.ShowAsync();

            if (d.Result is AddToCollectionResult result && result.Success)
            {
                SelectCollection(result.Collection);
            }
        }

        void SelectCollection(UserFontCollection collection)
        {
            ViewModel.Activate();
            ViewModel.SelectedCollection = collection;
        }

        private async void RenameFontCollection_Click(object sender, RoutedEventArgs e)
        {
            await (new CreateCollectionDialog(ViewModel.SelectedCollection)).ShowAsync();
            SelectCollection(ViewModel.SelectedCollection);
        }

        private void DeleteCollection_Click(object sender, RoutedEventArgs e)
        {
            var d = new ContentDialog
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
            await ViewModel.CollectionService.DeleteCollectionAsync(ViewModel.SelectedCollection);
            CollectionSelector.SelectedItem = null;
            CollectionSelector.SelectedIndex = -1;
            //SelectCollection(null);

            ViewModel.Messenger.Send(new AppNotificationMessage(true, $"\"{name}\" collection deleted"));
        }

        private string GetCountLabel(int fontCount, int selectedCount)
        {
            return string.Format(Localization.Get("FontsSelectedCountLabel"), fontCount, selectedCount);
        }
    }
}
