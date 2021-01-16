using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Provider;
using Microsoft.Toolkit.Mvvm.Messaging;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Storage;
using Windows.UI.Xaml;

namespace CharacterMap.Core
{
    public class AppSettings : INotifyPropertyChanged
    {
        public const int MinGridSize = 64;
        public const int MaxGridSize = 192;

        public double PngSize
        {
            get => Get(1024.00d);
            set => Set(value);
        }

        public int LastSelectedCharIndex
        {
            get => Get(0);
            set => Set(value);
        }

        public string LastSelectedFontName
        {
            get => Get(string.Empty);
            set => Set(value);
        }

        public bool FitCharacter
        {
            get => Get(false);
            set => BroadcastSet(value);
        }

        public bool IsTransparencyEnabled
        {
            get => Get(true);
            set { if (Set(value)) UpdateTransparency(value); }
        }

        public bool UseInstantSearch
        {
            get => Get(true);
            set => BroadcastSet(value);
        }

        public int InstantSearchDelay
        {
            get => Get(500);
            set => Set(value);
        }

        public int MaxSearchResult
        {
            get => Get(26);
            set => Set(value);
        }

        public double LastColumnWidth
        {
            get => Get(326d);
            set => Set(value);
        }

        public bool UseSelectionAnimations
        {
            get => Get(true);
            set => Set(value);
        }

        public bool EnableShadows
        {
            get => Get(true);
            set => Set(value);
        }

        public bool AllowExpensiveAnimations
        {
            get => Get(false);
            set => BroadcastSet(value);
        }

        public bool UseFontForPreview
        {
            get => Get(true);
            set => BroadcastSet(value);
        }

        public ElementTheme UserRequestedTheme
        {
            get => (ElementTheme)Get((int)ElementTheme.Default);
            set => BroadcastSet((int)value);
        }

        public GlyphAnnotation GlyphAnnotation
        {
            get => (GlyphAnnotation)Get((int)GlyphAnnotation.UnicodeHex);
            set => BroadcastSet((int)value);
        }

        public int GridSize
        {
            get => Get(80);
            set
            {
                if (Set(Math.Clamp(value, MinGridSize, MaxGridSize)))
                    DebounceGrid();
            }
        }

        public string AppLanguage
        {
            get => Get("en-US");
            set => Set(value);
        }

        public ExportNamingScheme ExportNamingScheme
        {
            get => (ExportNamingScheme)Get((int)ExportNamingScheme.Optimised);
            set => Set((int)value);
        }

        public DevProviderType SelectedDevProvider
        {
            get => (DevProviderType)Get((int)DevProviderType.None);
            set => Set((int)value);
        }

        public bool EnablePreviewPane
        {
            get => Get(true);
            set => BroadcastSet(value);
        }

        public bool EnableCopyPane
        {
            get => Get(false);
            set => BroadcastSet(value);
        }

        // This setting has been deprecated.
        // Do not reuse this setting name.
        //public bool ShowDevUtils
        //{
        //    get => Get(true);
        //    set => BroadcastSet(value);
        //}

        // This setting has been deprecated.
        // Do not reuse this setting name.
        //public int DevToolsLanguage
        //{
        //    get => Get(0);
        //    set => BroadcastSet(value);
        //}


        /* INFRASTRUCTURE */

        #region Infrastructure

        public event PropertyChangedEventHandler PropertyChanged;

        private Debouncer _gridDebouncer { get; } = new Debouncer();

        public ApplicationDataContainer LocalSettings { get; }

        public AppSettings()
        {
            LocalSettings = ApplicationData.Current.LocalSettings;
        }

        private bool Set(object value, [CallerMemberName]string key = null)
        {
            if (LocalSettings.Values.TryGetValue(key, out object t) && t != null && t.Equals(value))
                return false;

            LocalSettings.Values[key] = value;
            NotifyPropertyChanged(key);
            return true;
        }

        private bool BroadcastSet(object value, [CallerMemberName]string key = null)
        {
            bool result = Set(value, key);
            if (result)
                WeakReferenceMessenger.Default.Send(new AppSettingsChangedMessage(key));
            return result;
        }

        private T Get<T>(T defaultValue, [CallerMemberName]string key = null)
        {
            if (LocalSettings.Values.TryGetValue(key, out object value) && value is T val)
                return val;

            if (defaultValue != null)
                return defaultValue;

           return default;
        }

        protected void NotifyPropertyChanged([CallerMemberName]string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        private void DebounceGrid()
        {
            _gridDebouncer.Debounce(1000, () =>
            {
                WeakReferenceMessenger.Default.Send(new AppSettingsChangedMessage(nameof(GridSize)));
            });
        }

        public void UpdateTransparency(bool value)
        {
            _ = ResourceHelper.SetTransparencyAsync(value);
        }

        /// <summary>
        /// Apply an offset to the GridSize without a delay.
        /// </summary>
        /// <param name="change"></param>
        public void ChangeGridSize(int change)
        {
            if (Set(Math.Clamp(GridSize + change, MinGridSize, MaxGridSize), nameof(GridSize)))
            {
                WeakReferenceMessenger.Default.Send(new AppSettingsChangedMessage(nameof(GridSize)));
            }
        }

        #endregion
    }
}
