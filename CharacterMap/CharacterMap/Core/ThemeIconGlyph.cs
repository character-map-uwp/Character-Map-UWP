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

public enum ThemeIcon
{
    None,
    Cancel,
    Copy,
    Settings,
    Save,
    SaveAs,
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
    Back,

    ViewClose,

    FilledSquare,

    // TODO
    NewTab,
    CharacterMapView,
    TypeRampView,
    Calligraphy,
    RenderingOptions,
    CompareFonts,
    Collections,

    EnableGrouping,
    IncreaseFontSize,
    DecreaseFontSize,
    RightPane,
    BottomPane,

    SelectAll,
    Reset,

    Fullscreen,
    ExitFullscreen,
    GridView,
    ListView,
    SortOrderToggle,

    ClearSelection,
    ClearText,

    DragIndicator,
    DragDropHint,

    Layout,
    LookAndFeel,
    Advanced,
    Export,
    FontManagement,
    Changelog,
    Licenses,
    About
}

[MarkupExtensionReturnType(ReturnType = typeof(IconElement))]
public class ThemeIconElement : MarkupExtension
{
    public ThemeIcon Icon { get; set; }

    protected override object ProvideValue()
    {
        FontIcon icon = new();
        Core.Properties.SetThemeIcon(icon, Icon);
        return icon;
    }

}

[MarkupExtensionReturnType(ReturnType = typeof(string))]
public class ThemeIconGlyph : MarkupExtension
{
    public ThemeIcon Icon { get; set; }

    static string V(int i) => ((char)i).ToString();
    static string V(Symbol s) => V((int)s);

    /// <summary>
    /// MDL2 icon values
    /// </summary>
    static Dictionary<ThemeIcon, string> _mdl2Icons = new()
    {
        [ThemeIcon.Cancel] = V(Symbol.Cancel),
        [ThemeIcon.Copy] = V(Symbol.Copy),
        [ThemeIcon.Settings] = V(Symbol.Setting),
        [ThemeIcon.Save] = V(59214),
        [ThemeIcon.SaveAs] = V(0xe792),
        [ThemeIcon.GlobalMenu] = V(Symbol.GlobalNavigationButton),
        [ThemeIcon.FolderOpen] = V(59448),
        [ThemeIcon.FileOpen] = V(59621),
        [ThemeIcon.FontInfo] = V(59718),
        [ThemeIcon.ViewSwitch] = V(59563),
        [ThemeIcon.ChevronDown] = V(59149),
        [ThemeIcon.Filter] = V(59164),
        [ThemeIcon.More] = V(Symbol.More),
        [ThemeIcon.Add] = V(Symbol.Add),
        [ThemeIcon.Close] = V(58916),
        [ThemeIcon.ViewClose] = V(58916),
        [ThemeIcon.Search] = V(Symbol.Find),
        [ThemeIcon.Print] = V(Symbol.Print),
        [ThemeIcon.NewWindow] = V(0xe78b),
        [ThemeIcon.NewTab] = V(Symbol.NewWindow),
        [ThemeIcon.ClosePane] = V(Symbol.ClosePane),
        [ThemeIcon.ChevronRight] = V(Symbol.AlignRight),
        [ThemeIcon.Delete] = V(Symbol.Delete),
        [ThemeIcon.NewTab] = V(60621),
        [ThemeIcon.Calligraphy] = V(60923),
        [ThemeIcon.RenderingOptions] = V(0xE8D2),
        [ThemeIcon.Collections] = V(59165),
        [ThemeIcon.Remove] = V(0xe108),
        [ThemeIcon.CharacterMapView] = V(0xE8A9),
        [ThemeIcon.TypeRampView] = V(0xEA37),
        [ThemeIcon.About] = V(0xE946),
        [ThemeIcon.Back] = V(0xE72B),

        [ThemeIcon.Fullscreen] = V(0xE740),
        [ThemeIcon.ExitFullscreen] = V(0xE73F),
        [ThemeIcon.GridView] = V(0xF0E2),
        [ThemeIcon.ListView] = V(0xE8FD),
        [ThemeIcon.SortOrderToggle] = V(0xE8CB),

        [ThemeIcon.FilledSquare] = V(0xE73B),
        [ThemeIcon.DragIndicator] = V(0xE784),
        [ThemeIcon.DragDropHint] = V(0xE7C9),




        [ThemeIcon.IncreaseFontSize] = V(0xE8E8),
        [ThemeIcon.DecreaseFontSize] = V(0xE8E7),
        [ThemeIcon.BottomPane] = V(0xE75B),
        [ThemeIcon.RightPane] = V(0xE90D),
        [ThemeIcon.EnableGrouping] = V(0xF168),

        [ThemeIcon.ClearSelection] = V(0xE8E6),
        [ThemeIcon.ClearText] = V(0xED62),
        [ThemeIcon.SelectAll] = V(0xE8B3),
        [ThemeIcon.Reset] = V(0xE72C),

        [ThemeIcon.CompareFonts] = V(0xE8F1),

        [ThemeIcon.Advanced] = V(0xEA86),
        [ThemeIcon.Export] = V(0xE8A7),
        [ThemeIcon.LookAndFeel] = V(0xe771),
        [ThemeIcon.FontManagement] = V(0xe8d2),
        [ThemeIcon.Layout] = V(0xf3ea),
        [ThemeIcon.Licenses] = V(0xF571),
        [ThemeIcon.Changelog] = V(0xEC7A),
    };

    static Dictionary<ThemeIcon, string> _values = null;

    static Dictionary<ThemeIcon, string> _materialIcons = null;


    // Font is generated from this URL - open the top .WOFF2 font in CMUWP and export as OTF to 
    // bundle in the application
    // https://fonts.googleapis.com/css2?family=Material+Symbols+Rounded:opsz,wght,FILL,GRAD@20,400,0,0&icon_names=add,arrow_back,arrow_drop_down,arrow_right,change_history,check,close,close_fullscreen,collections_bookmark,content_copy,dashboard,delete,dock_to_bottom,dock_to_left,drag_indicator,draw,extension,file_open,filter_alt,filter_list,folder_open,globe_book,info,ios_share,license,local_library,low_priority,menu,more_horiz,new_window,open_in_full,print,remove,remove_selection,right_panel_open,save,search,setting,sort,style,sync,sync_alt,text_decrease,text_fields,text_increase,view_cozy,visibility

    private static Dictionary<ThemeIcon, string> GetMaterialMap()
    {
        return (_materialIcons ??= new()
        {
            [ThemeIcon.Cancel] = V(58829),
            [ThemeIcon.Copy] = V(57677),
            [ThemeIcon.Settings] = V(59576),
            [ThemeIcon.Save] = V(57697),
            [ThemeIcon.GlobalMenu] = V(58834),
            [ThemeIcon.FolderOpen] = V(58056),
            [ThemeIcon.FileOpen] = V(60147),
            [ThemeIcon.FontInfo] = V(59534),
            [ThemeIcon.ViewSwitch] = V(59928),
            [ThemeIcon.ChevronDown] = V(58821),
            [ThemeIcon.Filter] = V(61263),
            [ThemeIcon.More] = V(58835),
            [ThemeIcon.Add] = V(57669),
            [ThemeIcon.Close] = V(0xe14c),
            [ThemeIcon.ViewClose] = V(0xe5c4),
            [ThemeIcon.Search] = V(59574),
            [ThemeIcon.Print] = V(59565),
            [ThemeIcon.NewWindow] = V(63248),
            [ThemeIcon.ClosePane] = V(63236),
            [ThemeIcon.ChevronRight] = V(58847),
            [ThemeIcon.Delete] = V(59506),
            [ThemeIcon.Calligraphy] = V(59206),


            [ThemeIcon.CompareFonts] = V(0xe54b),
            [ThemeIcon.RenderingOptions] = V(0xe8f4),
            [ThemeIcon.CharacterMapView] = V(0xeb75),
            [ThemeIcon.Collections] = V(0xe431),
            [ThemeIcon.Remove] = V(0xe15b),
            [ThemeIcon.TypeRampView] = V(0xe152),
            [ThemeIcon.Back] = V(0xE5C4),

            [ThemeIcon.Fullscreen] = V(0xf1ce),
            [ThemeIcon.ExitFullscreen] = V(0xf1cf),

            [ThemeIcon.ClearSelection] = V(0xe9d5),
            [ThemeIcon.ClearText] = V(0xe9d5),

            [ThemeIcon.EnableGrouping] = V(0xe16d),
            [ThemeIcon.IncreaseFontSize] = V(60130),
            [ThemeIcon.DecreaseFontSize] = V(60125),
            [ThemeIcon.RightPane] = V(0xf7e5),
            [ThemeIcon.BottomPane] = V(0xf7e6),

            [ThemeIcon.DragIndicator] = V(0xe945),

            [ThemeIcon.Advanced] = V(0xe87b),
            [ThemeIcon.LookAndFeel] = V(0xe41d),
            [ThemeIcon.Export] = V(0xe6b8),
            [ThemeIcon.FontManagement] = V(0xe262),
            [ThemeIcon.Layout] = V(0xf3ea),
            [ThemeIcon.Licenses] = V(0xeb04),
            [ThemeIcon.Changelog] = V(0xe86b),
            [ThemeIcon.About] = V(59534),
        });
    }

    static ThemeIconGlyph()
    {
        _values = _mdl2Icons;
    }

    public static void EnableMaterialIcons()
    {
        _values = GetMaterialMap();
    }

    public static string Get(ThemeIcon key)
    {
        if (key is ThemeIcon.None)
            return string.Empty;

        if (_values.TryGetValue(key, out var value)) { return value; }

        return "NULL";
    }

    public static (string glyph, bool isFallback) GetWithFallback(ThemeIcon key)
    {
        string val = Get(key);

        // If a secondary icon source failed, try to get MDL2 icon
        if (val == "NULL" 
            && _values != _mdl2Icons
            && _mdl2Icons.TryGetValue(key, out var value))
            return (value, true);

        return (val, false);
    }

    protected override object ProvideValue() => Get(Icon);
}
