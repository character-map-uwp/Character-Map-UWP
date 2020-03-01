using CharacterMap.Helpers;
using GalaSoft.MvvmLight.Messaging;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Storage;

namespace CharacterMap.Core
{
    public class AppSettings : INotifyPropertyChanged
    {
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

        public int GridSize
        {
            get => Get(80);
            set
            {
                if (Set(value))
                    DebounceGrid();
            }
        }

        public bool UseFontForPreview
        {
            get => Get(false);
            set
            {
                if (Set(value))
                    Messenger.Default.Send(new FontPreviewUpdatedMessage());
            }
        }

        public bool ShowDevUtils
        {
            get => Get(true);
            set => Set(value);
        }

        public bool UseInstantSearch
        {
            get => Get(true);
            set => Set(value);
        }

        public int InstantSearchDelay
        {
            get => Get(500);
            set => Set(value);
        }

        public int MaxSearchResult
        {
            get => Get(15);
            set => Set(value);
        }

        public double LastColumnWidth
        {
            get => Get(330d);
            set => Set(value);
        }




        /* INFRASTRUCTURE */

        public event PropertyChangedEventHandler PropertyChanged;

        private Debouncer _gridDebouncer { get; } = new Debouncer();

        public ApplicationDataContainer LocalSettings { get; }

        public AppSettings()
        {
            LocalSettings = ApplicationData.Current.LocalSettings;
        }

        private bool Set(object value, [CallerMemberName]string key = null)
        {
            if (LocalSettings.Values.TryGetValue(key, out object t) && t.Equals(value))
                return false;

            LocalSettings.Values[key] = value;
            NotifyPropertyChanged(key);
            return true;
        }

        private T Get<T>(T defaultValue, [CallerMemberName]string key = null)
        {
            if (LocalSettings.Values.TryGetValue(key, out object value))
                return (T)value;

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
                Messenger.Default.Send(new GridSizeUpdatedMessage());
            });
        }
    }
}
