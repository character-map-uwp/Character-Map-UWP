using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Services;
using CommonServiceLocator;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Messaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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

        public bool IsRenameMode { get; }

        private UserFontCollection _collection = null;

        public CreateCollectionDialog(UserFontCollection collection = null)
        {
            _collection = collection;
            TemplateSettings = new CreateCollectionDialogTemplateSettings();
            this.InitializeComponent();

            if (_collection != null)
            {
                IsRenameMode = true;
                this.Title = Localization.Get("DigRenameCollection/Title");
                this.PrimaryButtonText = Localization.Get("DigRenameCollection/PrimaryButtonText");
                TemplateSettings.CollectionTitle = _collection.Name;
            }
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var d = args.GetDeferral();

            var collections = ServiceLocator.Current.GetInstance<UserCollectionsService>();

            if (IsRenameMode)
            {
                this.IsPrimaryButtonEnabled = false;
                this.IsSecondaryButtonEnabled = false;
                InputBox.IsEnabled = false;

                await collections.RenameCollectionAsync(TemplateSettings.CollectionTitle, _collection);
                d.Complete();

                await Task.Yield();
                Messenger.Default.Send(new CollectionsUpdatedMessage());
            }
            else
            {
                var collection = await collections.CreateCollectionAsync(TemplateSettings.CollectionTitle);
                var result = await collections.AddToCollectionAsync(this.DataContext as InstalledFont, collection);
                d.Complete();

                await Task.Yield();
                if (result.Success)
                    Messenger.Default.Send(new AppNotificationMessage(true, result));

            }
        }
    }
}
