using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Storage;

namespace CharacterMap.Core
{
    public class AppSettings : INotifyPropertyChanged
    {
        public double PngSize
        {
            get => ReadSettings(nameof(PngSize), 256.00d);
            set
            {
                SaveSettings(nameof(PngSize), value);
                NotifyPropertyChanged();
            }
        }

        public bool UseDefaultSelection
        {
            get => ReadSettings(nameof(UseDefaultSelection), false);
            set
            {
                SaveSettings(nameof(UseDefaultSelection), value);
                NotifyPropertyChanged();
            }
        }

        public bool ShowSymbolFontsOnly
        {
            get => ReadSettings(nameof(ShowSymbolFontsOnly), false);
            set
            {
                SaveSettings(nameof(ShowSymbolFontsOnly), value);
                NotifyPropertyChanged();
            }
        }
        
        public string DefaultSelectedFontName
        {
            get => ReadSettings(nameof(DefaultSelectedFontName), string.Empty);
            set
            {
                SaveSettings(nameof(DefaultSelectedFontName), value);
                NotifyPropertyChanged();
            }
        }

        public int GridSize
        {
            get => ReadSettings(nameof(GridSize), 60);
            set
            {
                SaveSettings(nameof(GridSize), value);
                NotifyPropertyChanged();
            }
        }

        public int ImageWidthHeight
        {
            get => ReadSettings(nameof(ImageWidthHeight), 960);
            set
            {
                SaveSettings(nameof(ImageWidthHeight), value);
                NotifyPropertyChanged();
            }
        }

        public ApplicationDataContainer LocalSettings { get; set; }

        public AppSettings()
        {
            LocalSettings = ApplicationData.Current.LocalSettings;
        }

        private void SaveSettings(string key, object value)
        {
            LocalSettings.Values[key] = value;
        }

        private T ReadSettings<T>(string key, T defaultValue)
        {
            if (LocalSettings.Values.ContainsKey(key))
            {
                return (T)LocalSettings.Values[key];
            }
            if (null != defaultValue)
            {
                return defaultValue;
            }
            return default(T);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged([CallerMemberName]string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
