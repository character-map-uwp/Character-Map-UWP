using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;

namespace CharacterMap.Core;

[MarkupExtensionReturnType(ReturnType = typeof(object))]
public class OptionalResource : MarkupExtension
{
    public string Key { get; set; }

    protected override object ProvideValue()
    {
        if (ResourceHelper.TryGet(Key, out object t))
            return t;

        return null;
    }
}

public enum ThemeIcons
{
    None,
    Cancel,
    Copy,
    Settings,
    Save,
    GlobalMenu,
    FolderOpen,
    FileOpen,
    FontInfo,
    ViewSwitch,
    ChevronDown,
    ChevronRight,
    Filter,
    Add,
    Remove,
    Close,
    Search,
    Print,
    NewWindow,
    ClosePane,
    Delete,
    More,

    ViewClose,

    // TODO
    NewTab,
    CharacterMapView,
    TypeRampView,
    Calligraphy,
    RenderingOptions,
    CompareFonts,
    Collections,

    IncreaseFontSize,
    DecreaseFontSize,

    SelectAll,
    Reset,

    Fullscreen,
    ExitFullscreen,

    Layout,
    LookAndFeel,
    Advanced,
    Export,
    FontManagement,
    Changelog,
    Licenses,
    About
}

[MarkupExtensionReturnType(ReturnType = typeof(string))]
public class ThemeIcon : MarkupExtension
{
    public ThemeIcons Icon { get; set; }

    static string V(int i) => ((char)i).ToString();
    static string V(Symbol s) => V((int)s);

    /// <summary>
    /// MDL2 icon values
    /// </summary>
    static Dictionary<ThemeIcons, string> _values = new()
    {
        [ThemeIcons.Cancel] = V(Symbol.Cancel),
        [ThemeIcons.Copy] = V(Symbol.Copy),
        [ThemeIcons.Settings] = V(Symbol.Setting),
        [ThemeIcons.Save] = V(Symbol.SaveLocal),
        [ThemeIcons.GlobalMenu] = V(Symbol.GlobalNavigationButton),
        [ThemeIcons.FolderOpen] = V(59448),
        [ThemeIcons.FileOpen] = V(59621),
        [ThemeIcons.FontInfo] = V(59718),
        [ThemeIcons.ViewSwitch] = V(59563),
        [ThemeIcons.ChevronDown] = V(59149),
        [ThemeIcons.Filter] = V(59164),
        [ThemeIcons.More] = V(Symbol.More),
        [ThemeIcons.Add] = V(Symbol.Add),
        [ThemeIcons.Close] = V(58916),
        [ThemeIcons.Search] = V(Symbol.Find),
        [ThemeIcons.Print] = V(Symbol.Print),
        [ThemeIcons.NewWindow] = V(Symbol.NewWindow),
        [ThemeIcons.ClosePane] = V(Symbol.ClosePane),
        [ThemeIcons.ChevronRight] = V(Symbol.AlignRight),
        [ThemeIcons.Delete] = V(Symbol.Delete),
        [ThemeIcons.NewTab] = V(60621),
        [ThemeIcons.Calligraphy] = V(60923),
        [ThemeIcons.RenderingOptions] = V(0xE8D2),
        [ThemeIcons.Collections] = V(0xe17d),
        [ThemeIcons.Remove] = V(0xe108),
        [ThemeIcons.CharacterMapView] = V(0xE8A9),
        [ThemeIcons.TypeRampView] = V(0xEA37),
        [ThemeIcons.About] = V(0xE946),

        [ThemeIcons.Fullscreen] = V(0xE740),
        [ThemeIcons.ExitFullscreen] = V(0xE73F),

        [ThemeIcons.IncreaseFontSize] = V(0xE8E8),
        [ThemeIcons.DecreaseFontSize] = V(0xE8E7),
        [ThemeIcons.CompareFonts] = V(0xE8F1),

        [ThemeIcons.Advanced] = V(0xEA86),
        [ThemeIcons.Export] = V(0xE8A7),
        [ThemeIcons.LookAndFeel] = V(0xe771),
        [ThemeIcons.FontManagement] = V(0xe8d2),
        [ThemeIcons.Layout] = V(0xf3ea),
        [ThemeIcons.Licenses] = V(0xF571),
        [ThemeIcons.Changelog] = V(0xEC7A),
    };

    // Font is generated from this URL - open the top .WOFF2 font in CMUWP and export as OTF to 
    // bundle in the application
    // https://fonts.googleapis.com/css2?family=Material+Symbols+Rounded:opsz,wght,FILL,GRAD@20,400,0,0&icon_names=add,arrow_back,arrow_drop_down,arrow_right,change_history,check,close,close_fullscreen,collections_bookmark,content_copy,dashboard,delete,draw,extension,file_open,filter_alt,filter_list,folder_open,globe_book,info,ios_share,license,local_library,menu,more_horiz,new_window,open_in_full,print,remove,right_panel_open,save,search,setting,sort,style,sync,sync_alt,text_decrease,text_fields,text_increase,view_cozy,visibility
    public static void EnableMaterialIcons()
    {
        _values = new()
        {
            [ThemeIcons.Cancel] = V(58829),
            [ThemeIcons.Copy] = V(57677),
            [ThemeIcons.Settings] = V(59576),
            [ThemeIcons.Save] = V(57697),
            [ThemeIcons.GlobalMenu] = V(58834),
            [ThemeIcons.FolderOpen] = V(58056),
            [ThemeIcons.FileOpen] = V(60147),
            [ThemeIcons.FontInfo] = V(59534),
            [ThemeIcons.ViewSwitch] = V(59928),
            [ThemeIcons.ChevronDown] = V(58821),
            [ThemeIcons.Filter] = V(61263),
            [ThemeIcons.More] = V(58835),
            [ThemeIcons.Add] = V(57669),
            [ThemeIcons.Close] = V(0xe14c),
            [ThemeIcons.ViewClose] = V(0xe5c4),
            [ThemeIcons.Search] = V(59574),
            [ThemeIcons.Print] = V(59565),
            [ThemeIcons.NewWindow] = V(63248),
            [ThemeIcons.ClosePane] = V(63236),
            [ThemeIcons.ChevronRight] = V(58847),
            [ThemeIcons.Delete] = V(59506),
            [ThemeIcons.Calligraphy] = V(59206),
            [ThemeIcons.IncreaseFontSize]= V(60130),
            [ThemeIcons.DecreaseFontSize]= V(60125),
            [ThemeIcons.CompareFonts] = V(0xe54b),
            [ThemeIcons.RenderingOptions] = V(0xe8f4),
            [ThemeIcons.CharacterMapView] = V(0xeb75),
            [ThemeIcons.Collections] = V(0xe431),
            [ThemeIcons.Remove] = V(0xe15b),
            [ThemeIcons.TypeRampView] = V(0xe152),

            [ThemeIcons.Fullscreen] = V(0xf1ce), 
            [ThemeIcons.ExitFullscreen] = V(0xf1cf),

            [ThemeIcons.Advanced] = V(0xe87b),
            [ThemeIcons.LookAndFeel] = V(0xe41d),
            [ThemeIcons.Export] = V(0xe6b8),
            [ThemeIcons.FontManagement] = V(0xe262),
            [ThemeIcons.Layout]= V(0xf3ea),
            [ThemeIcons.Licenses] = V(0xeb04),
            [ThemeIcons.Changelog] = V(0xe86b),
            [ThemeIcons.About] = V(59534)
        };
    }

    public static string Get(ThemeIcons key)
    {
        if (_values.TryGetValue(key, out var value)) { return value; }

        return "NULL";
    }

    protected override object ProvideValue() => Get(Icon);
}
