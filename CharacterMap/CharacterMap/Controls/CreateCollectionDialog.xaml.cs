using CharacterMap.Core;
using CharacterMap.Services;
using CommonServiceLocator;
using GalaSoft.MvvmLight;
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
    public class CreateCollectionDialogTemplateSettings : ViewModelBase
    {
        private string _collectionTitle;
        public string CollectionTitle
        {
            get => _collectionTitle;
            set { if (Set(ref _collectionTitle, value)) OnCollectionTitleChanged(); }
        }

        private bool _isCollectionTitleValid;
        public bool IsCollectionTitleValid
        {
            get => _isCollectionTitleValid;
            private set => Set(ref _isCollectionTitleValid, value);
        }

        private void OnCollectionTitleChanged()
        {
            IsCollectionTitleValid = !string.IsNullOrWhiteSpace(CollectionTitle);
        }
    }

    public sealed partial class CreateCollectionDialog : ContentDialog
    {
        public CreateCollectionDialogTemplateSettings TemplateSettings { get; }

        public CreateCollectionDialog()
        {
            TemplateSettings = new CreateCollectionDialogTemplateSettings();
            this.InitializeComponent();
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var d = args.GetDeferral();
            var collections = ServiceLocator.Current.GetInstance<UserCollectionsService>();
            var collection = await collections.CreateCollectionAsync(TemplateSettings.CollectionTitle);
            await collections.AddToCollectionAsync(this.DataContext as InstalledFont, collection);
            d.Complete();
        }
    }
}
