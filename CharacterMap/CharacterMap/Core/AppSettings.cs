using System.ComponentModel;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;

namespace CharacterMap.Core;

public class AppSettings : INotifyPropertyChanged
{
    public const int MinGridSize = 48;
    public const int MaxGridSize = 192;
    public string StartupLanugage { get; }

    public const string UserCollectionIdentifier = "||UC:||";
    public const string FAMILY_PREVIEW_TOOLTIP = "The quick brown dog jumps over the lazy fox. 123456!?";

    public string FileNameTemplate
    {
        get => Get<string>(ExportOptions.DefaultTemplate);
        set => Set(value);
    }

    /// <summary>
    /// Last opened collection in MainView
    /// </summary>
    public string LastSelectedCollection
    {
        get => Get<string>(null);
        set => Set(value);
    }

    public bool RestoreLastCollectionOnLaunch
    {
        get => Get(false);
        set => Set(value);
    }

    public int SettingsVersion
    {
        get => Get(0);
        set => Set(value);
    }

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

    public bool HasSQLiteCollections
    {
        get => Get(false);
        set => Set(value);
    }

    public bool GroupCharacters
    {
        get => Get(true);
        set => BroadcastSet(value);
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

    public int ApplicationDesignTheme
    {
        get => Get(0);
        set => BroadcastSet(value);
    }


    public int FontListFontSizeIndex
    {
        get => Get(0);
        set => BroadcastSet(value);
    }

    public int MaxSearchResult
    {
        get => Get(101, "MSR");
        set => Set(value, "MSR");
    }

    public double LastColumnWidth
    {
        get => Get(326d);
        set => Set(value);
    }

    /// <summary>
    /// Use basic in-app animation.
    /// Try to disable all animation if this is off
    /// </summary>
    public bool UseSelectionAnimations
    {
        get => Get(true);
        set => BroadcastSet(value);
    }

    public bool UseFluentPointerOverAnimations1
    {
        get => Get(true);
        set => BroadcastSet(value);
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

    public bool HideDeprecatedMDL2
    {
        get => Get(true);
        set => Set(value);
    }

    public bool HideSimulatedFontFaces
    {
        get => Get(false);
        set => Set(value);
    }

    public ElementTheme UserRequestedTheme
    {
        get => (ElementTheme)Get((int)ElementTheme.Default);
        set { if (BroadcastSet((int)value)) UpdateTheme(); }
    }

    public GlyphAnnotation GlyphAnnotation
    {
        get => (GlyphAnnotation)Get((int)GlyphAnnotation.None);
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
        get => Get("", "AppLang2");
        set => Set(value, "AppLang2");
    }

    public string FamilyPreviewString
    {
        get => Get(FAMILY_PREVIEW_TOOLTIP);
        set => Set(value);
    }

    public string GetFamilyPreviewString()
    {
        if (string.IsNullOrWhiteSpace(FamilyPreviewString))
            return FAMILY_PREVIEW_TOOLTIP;
        return FamilyPreviewString;
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
        get => Get(true);
        set => BroadcastSet(value);
    }


    // Currently Unused
    public bool DisableTabs
    {
        get => Get(false);
        set => BroadcastSet(value);
    }

    public IList<string> LastOpenFonts
    {
        get => GetStrings();
        set => Set(value);
    }

    public IList<string> CustomRampOptions
    {
        get => GetStrings();
        set => Set(value);
    }

    public int LastTabIndex
    {
        get => Get(0);
        set => Set(value);
    }

    public bool FontExportIncludeVersion
    {
        get => Get(false);
        set => Set(value);
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

    /// <summary>
    /// Get the supported UserLanguage, taking into account the actually
    /// support languages.
    /// </summary>
    /// <returns></returns>
    public string GetUserLanguageID()
    {
        if (!string.IsNullOrWhiteSpace(AppLanguage))
        {
            if (SettingsViewModel.GetSupportedLanguages()
                .FirstOrDefault(id => id.LanguageID == AppLanguage) is SupportedLanguage lang)
                return lang.LanguageID;
            else
            {
                // The chosen language is no longer supported by the app.
                // Remove the saved setting value.
                LocalSettings.Values.Remove("AppLang2");
            }
        }

        return null;
    }




    /* INFRASTRUCTURE */

    #region Infrastructure

    #region Context-aware PropertyChanged

    private object _lock { get; } = new();

    private Dictionary<SynchronizationContext, PropertyChangedEventHandler> _handlerCache { get; } = new();

    public event PropertyChangedEventHandler PropertyChanged
    {
        add
        {
            if (value == null)
                return;

            var ctx = SynchronizationContext.Current;
            lock (_lock)
            {
                if (_handlerCache.TryGetValue(ctx, out PropertyChangedEventHandler eventHandler))
                {
                    eventHandler += value;
                    _handlerCache[ctx] = eventHandler;
                }
                else
                    _handlerCache.Add(ctx, value);
            }
        }
        remove
        {
            if (value == null)
                return;

            var ctx = SynchronizationContext.Current;
            lock (_lock)
            {
                if (_handlerCache.TryGetValue(ctx, out PropertyChangedEventHandler eventHandler))
                {
                    eventHandler -= value;
                    if (eventHandler != null)
                        _handlerCache[ctx] = eventHandler;
                    else
                        _handlerCache.Remove(ctx);
                }
            }
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        KeyValuePair<SynchronizationContext, PropertyChangedEventHandler>[] handlers;
        lock (_lock)
            handlers = _handlerCache.ToArray();

        PropertyChangedEventArgs eventArgs = new(propertyName);
        foreach (var handler in handlers)
        {
            void Do() => handler.Value(this, eventArgs);

            if (SynchronizationContext.Current == handler.Key)
                Do();
            else
                handler.Key.Post(o => Do(), null);
        }
    }

    #endregion

    private Debouncer _gridDebouncer { get; } = new Debouncer();

    public ApplicationDataContainer LocalSettings { get; }

    public AppSettings()
    {
        LocalSettings = ApplicationData.Current.LocalSettings;
        StartupLanugage = AppLanguage;
        UpdateSettings();
    }

    private bool Set(IList<string> value, [CallerMemberName] string key = null)
    {
        string s = string.Join(Environment.NewLine, value);
        return Set(s, key);
    }

    private bool Set(object value, [CallerMemberName] string key = null)
    {
        if (LocalSettings.Values.TryGetValue(key, out object t) && t != null && t.Equals(value))
            return false;

        LocalSettings.Values[key] = value;
        NotifyPropertyChanged(key);
        return true;
    }

    private bool BroadcastSet(object value, [CallerMemberName] string key = null)
    {
        bool result = Set(value, key);
        if (result)
            WeakReferenceMessenger.Default.Send(new AppSettingsChangedMessage(key));
        return result;
    }

    private List<string> GetStrings([CallerMemberName] string key = null)
    {
        string data = Get<string>(null, key);
        if (string.IsNullOrWhiteSpace(data))
            return new List<string>();

        return data.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private T Get<T>(T defaultValue, [CallerMemberName] string key = null)
    {
        if (LocalSettings.Values.TryGetValue(key, out object value) && value is T val)
            return val;

        if (defaultValue != null)
            return defaultValue;

        return default;
    }

    protected void NotifyPropertyChanged([CallerMemberName] string propName = "")
    {
        OnPropertyChanged(propName);
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

    public void UpdateTheme()
    {
        _ = WindowService.RunOnViewsAsync(() =>
        {
            if (Window.Current.Content is FrameworkElement e)
                e.RequestedTheme = ResourceHelper.GetEffectiveTheme();
        });
    }

    private void UpdateSettings()
    {
        if (SettingsVersion == 0)
        {
            // Upgrade to Version 1.0.

            // 1. Check version of Windows. If Windows 11, default to the Windows 11 theme
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 14))
                ApplicationDesignTheme = (int)DesignStyle.Fluent11;

            SettingsVersion = 1;
        }
    }

    public void IncreaseFontListSize()
    {
        if (FontListFontSizeIndex < (int)FontListFontSize.Larger)
            FontListFontSizeIndex++;
    }

    public void DecreaseFontListSize()
    {
        if (FontListFontSizeIndex > 0)
            FontListFontSizeIndex--;
    }

    #endregion
}
