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


[MarkupExtensionReturnType(ReturnType = typeof(string))]
public class Icons : MarkupExtension
{
    public string Icon { get; set; }

    static string V(int i) => ((char)i).ToString();
    static string V(Symbol s) => V((int)s);

    /// <summary>
    /// MDL2 icon values
    /// </summary>
    static Dictionary<string, string> _values = new()
    {
        [nameof(Symbol.Cancel)] = V(Symbol.Cancel),
        [nameof(Symbol.Copy)] = V(Symbol.Copy), 
        ["Settings"] = V(Symbol.Setting),
        ["Save"] = V(Symbol.SaveLocal),
        ["Menu"] = V(Symbol.GlobalNavigationButton),
        ["FolderOpen"] = V(59448),
        ["FileOpen"] = V(59621),
        ["Info"] = V(59718),
        ["Switch"] = V(59563),
        ["ChevronDown"] = V(59149),
        ["Filter"] = V(59164),
        ["More"] = V(Symbol.More),
        ["Add"] = V(Symbol.Add),
        ["Close"] = V(58916),
        ["Search"] = V(Symbol.Find),
        ["Print"] = V(Symbol.Print),
        ["NewWindow"] = V(Symbol.NewWindow),
        ["ClosePane"] = V(Symbol.ClosePane),
        ["ChevronRight"] = V(Symbol.AlignRight),
        ["Delete"] = V(Symbol.Delete),
    };

    // https://fonts.googleapis.com/css2?family=Material+Symbols+Rounded:opsz,wght,FILL,GRAD@20,400,0,0&icon_names=add,arrow_back,arrow_drop_down,arrow_right,check,close,content_copy,delete,file_open,filter_alt,folder_open,globe_book,info,menu,more_horiz,new_window,print,right_panel_open,save,search,setting,sync,sync_alt
    public static void EnableMaterialIcons()
    {
        _values = new()
        {
            [nameof(Symbol.Cancel)] = V(58829),
            [nameof(Symbol.Copy)] = V(57677),
            ["Settings"] = V(59576),
            ["Save"] = V(57697),
            ["Menu"] = V(58834),
            ["FolderOpen"] = V(58056),
            ["FileOpen"] = V(60147),
            ["Info"] = V(59534),
            ["Switch"] = V(59928),
            ["ChevronDown"] = V(58821),
            ["Filter"] = V(61263),
            ["More"] = V(58835),
            ["Add"] = V(57669),
            ["Close"] = V(57676),
            ["Search"] = V(59574),
            ["Print"] = V(59565),
            ["NewWindow"] = V(63248),
            ["ClosePane"] = V(63236),
            ["ChevronRight"] = V(58847),
            ["Delete"] = V(59506),
        };
    }

    public static string Get(string key)
    {
        if (_values.TryGetValue(key, out var value)) { return value; }

        return "NULL";
    }

    protected override object ProvideValue() => Get(Icon);
}
