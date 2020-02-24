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
            get => Read(1024.00d);
            set => SaveAndNotify(value);
        }

        public int LastSelectedCharIndex
        {
            get => Read(0);
            set => SaveAndNotify(value);
        }

        public string LastSelectedFontName
        {
            get => Read(string.Empty);
            set => SaveAndNotify(value);
        }

        public int GridSize
        {
            get => Read(96);
            set
            {
                SaveAndNotify(value);
                DebounceGrid();
            }
        }

        public bool ShowDevUtils
        {
            get => Read(true);
            set => SaveAndNotify(value);
        }

        public bool UseFontForPreview
        {
            get => Read(false);
            set => SaveAndNotify(value);
        }

        public bool UseInstantSearch
        {
            get => Read(true);
            set => SaveAndNotify(value);
        }

        public int InstantSearchDelay
        {
            get => Read(500);
            set => SaveAndNotify(value);
        }

        public int MaxSearchResult
        {
            get => Read(15);
            set => SaveAndNotify(value);
        }




        /* INFRASTRUCTURE */

        public event PropertyChangedEventHandler PropertyChanged;

        private Debouncer _gridDebouncer { get; } = new Debouncer();

        public ApplicationDataContainer LocalSettings { get; }

        public AppSettings()
        {
            LocalSettings = ApplicationData.Current.LocalSettings;
        }

        private void SaveAndNotify(object value, [CallerMemberName]string key = null)
        {
            LocalSettings.Values[key] = value;
            NotifyPropertyChanged(key);
        }

        private T Read<T>(T defaultValue, [CallerMemberName]string key = null)
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
