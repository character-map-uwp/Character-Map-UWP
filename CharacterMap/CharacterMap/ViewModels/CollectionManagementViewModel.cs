using CharacterMap.Core;
using CharacterMap.Models;
using CharacterMap.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using CharacterMap.Helpers;

namespace CharacterMap.ViewModels
{
    internal class CollectionManagementViewModel : ViewModelBase
    {
        #region Properties

        public bool IsSaving                        { get => GetV(false); set => Set(value); }
        public bool IsExporting                     { get => GetV(false); set => Set(value); }

        public ObservableCollection<InstalledFont> SelectedFonts            = new();

        public ObservableCollection<InstalledFont> SelectedCollectionFonts  = new();

        public ObservableCollection<InstalledFont> FontList
        {
            get => Get<ObservableCollection<InstalledFont>>();
            set => Set(value);
        }

        public ObservableCollection<InstalledFont> CollectionFonts
        {
            get => Get<ObservableCollection<InstalledFont>>();
            set => Set(value);
        }

        public List<UserFontCollection> Collections
        {
            get => Get<List<UserFontCollection>>();
            set => Set(value);
        }

        private UserFontCollection _selectedCollection;
        public UserFontCollection SelectedCollection
        {
            get => _selectedCollection;
            set
            {
                if (Set(ref _selectedCollection, value) && value != null)
                    RefreshFontLists();
            }
        }

        private UserCollectionsService _collectionService = null;

        #endregion




        public void Activate()
        {
            if (_collectionService is null)
                _collectionService = Ioc.Default.GetService<UserCollectionsService>();

            RefreshCollections();
            RefreshFontLists();
        }

        public void Deactivate()
        {
            _ = SaveAsync();
            SelectedCollection = null;
            RefreshFontLists();
        }

        void RefreshCollections()
        {
            Collections = _collectionService.Items;
        }

        public void RefreshFontLists()
        {
            if (SelectedCollection is null)
            {
                // clear all the things
                CollectionFonts = new();
                FontList = new ();
                return;
            }

            // 1. Get list of fonts in and not in the collection
            var collectionFonts = FontFinder.Fonts.Where(f => SelectedCollection.Fonts.Contains(f.Name)).ToList();
            var systemFonts = FontFinder.Fonts.Except(collectionFonts).ToList();

            // 2. Create binding lists
            FontList = new (systemFonts);
            CollectionFonts = new (collectionFonts);
        }

        public void AddToCollection()
        {
            if (SelectedFonts is null || SelectedFonts.Count == 0)
                return;

            var fonts = SelectedFonts.ToList();
            foreach (var font in fonts)
                if (FontList.Remove(font))
                    CollectionFonts.AddSorted(font);

            StartSave();
        }

        public void RemoveFromCollection()
        {
            if (SelectedCollectionFonts is null || SelectedCollectionFonts.Count == 0)
                return;

            var fonts = SelectedCollectionFonts.ToList();
            foreach (var font in fonts)
                if (CollectionFonts.Remove(font))
                    FontList.AddSorted(font);

            StartSave();
        }

        public void StartSave()
        {
            _ = SaveAsync();
        }

        async Task SaveAsync()
        {
            if (SelectedCollection is null || IsSaving)
                return;

            IsSaving = true;

            try
            {
                SelectedCollection.Fonts = new HashSet<string>(CollectionFonts.Select(c => c.Name));
                await _collectionService.SaveCollectionAsync(SelectedCollection);
            }
            finally
            {
                IsSaving = false;
            }
        }

        internal async void ExportAsZip()
        {
            IsExporting = true;

            try
            {
                await ExportManager.ExportCollectionAsZipAsync(CollectionFonts, SelectedCollection);
            }
            finally
            {
                IsExporting = false;
            }
        }

        internal async void ExportAsFolder()
        {
            IsExporting = true;

            try
            {
                await ExportManager.ExportCollectionToFolderAsync(CollectionFonts);
            }
            finally
            {
                IsExporting = false;
            }
        }
    }
}
