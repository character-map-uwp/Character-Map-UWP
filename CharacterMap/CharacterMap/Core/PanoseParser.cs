using System.Text.RegularExpressions;

namespace CharacterMap.Core;

public class Panose
{
    static Regex _r = new Regex(@"
                (?<=[A-Z])(?=[A-Z][a-z]) |
                 (?<=[^A-Z])(?=[A-Z]) |
                 (?<=[A-Za-z])(?=[^A-Za-z])", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

    private readonly DWriteProperties _props;

    public bool IsSansSerifStyle { get; }
    public bool IsSerifStyle { get; }
    public PanoseSerifStyle Style { get; }
    public PanoseFamily Family { get; }

    public bool HasPanose { get; }

    Dictionary<string, string> _values = null;
    public IReadOnlyDictionary<string, string> GetValues()
        => _values ??= SetValues(_props.Panose);

    public Panose(PanoseFamily family, PanoseSerifStyle style, DWriteProperties props, bool hasPanose = true)
    {
        _props = props;

        HasPanose = hasPanose && (family != PanoseFamily.Any && style != PanoseSerifStyle.ANY);
        Family = family;
        Style = style;
        IsSansSerifStyle = Style >= PanoseSerifStyle.NORMAL_SANS;
        IsSerifStyle =
            Style != PanoseSerifStyle.ANY
            && Style != PanoseSerifStyle.NO_FIT
            && Family != PanoseFamily.No_Fit
            && Family != PanoseFamily.Script
            && Family != PanoseFamily.Decorative
            && !IsSansSerifStyle;
    }

    Dictionary<string, string> SetValues(byte[] bytes)
    {
        Dictionary<string, string> values = new();
        StringBuilder sb = new();

        void Add<T>(string name, int idx) where T : Enum
        {
            var eType = typeof(T);
            values.Add(_r.Replace(name.Remove(0, 6), " "),
                Humanise(Enum.GetName(eType, (T)(object)(int)bytes[idx])));
        }
        string Humanise(string value)
        {
            if (value == null)
                return value;

            sb.Clear();

            bool caps = true;
            foreach (var c in value)
            {
                if (c == '_')
                {
                    sb.Append(" ");
                    caps = true;
                    continue;
                }

                if (caps)
                    sb.Append(char.ToUpper(c));
                else
                    sb.Append(char.ToLower(c));
                caps = false;
            }

            return sb.ToString();
        }

        if (Family == PanoseFamily.Text_Display)
        {
            Add<PanoseFamily>(nameof(PanoseFamily), 0);
            Add<PanoseSerifStyle>(nameof(PanoseSerifStyle), 1);
            Add<PanoseWeight>(nameof(PanoseWeight), 2);
            Add<PanoseProportion>(nameof(PanoseProportion), 3);
            Add<PanoseContrast>(nameof(PanoseContrast), 4);
            Add<PanoseStrokeVariation>(nameof(PanoseStrokeVariation), 5);
            Add<PanoseArmStyle>(nameof(PanoseArmStyle), 6);
            Add<PanoseLetterform>(nameof(PanoseLetterform), 7);
            Add<PanoseMidline>(nameof(PanoseMidline), 8);
            Add<PanoseXHeight>(nameof(PanoseXHeight), 9);
        }
        else if (Family == PanoseFamily.Script)
        {
            Add<PanoseFamily>(nameof(PanoseFamily), 0);
            Add<PanoseToolKind>(nameof(PanoseToolKind), 1);
            Add<PanoseWeight>(nameof(PanoseWeight), 2);
            Add<PanoseSpacing>(nameof(PanoseSpacing), 3);
            Add<PanoseAspectRatio>(nameof(PanoseAspectRatio), 4);
            Add<PanoseContrast>(nameof(PanoseContrast), 5);
            Add<PanoseScriptTopology>(nameof(PanoseScriptTopology), 6);
            Add<PanoseScriptForm>(nameof(PanoseScriptForm), 7);
            Add<PanoseFinials>(nameof(PanoseFinials), 8);
            Add<PanoseXAscent>(nameof(PanoseXAscent), 9);
        }
        else if (Family == PanoseFamily.Decorative)
        {
            Add<PanoseFamily>(nameof(PanoseFamily), 0);
            Add<PanoseDecorativeClass>(nameof(PanoseDecorativeClass), 1);
            Add<PanoseWeight>(nameof(PanoseWeight), 2);
            Add<PanoseAspect>(nameof(PanoseAspect), 3);
            Add<PanoseContrast>(nameof(PanoseContrast), 4);
            //Add<PanoseContrast>(nameof(PanoseContrast), 5);
            Add<PanoseFill>(nameof(PanoseFill), 6);
            Add<PanoseLining>(nameof(PanoseLining), 7);
            Add<PanoseDecorativeTopology>(nameof(PanoseDecorativeTopology), 8);
            Add<PanoseCharacterRanges>(nameof(PanoseCharacterRanges), 9);
        }
        else if (Family == PanoseFamily.Symbol)
        {
            Add<PanoseFamily>(nameof(PanoseFamily), 0);
            Add<PanoseSymbolKind>(nameof(PanoseDecorativeClass), 1);
            Add<PanoseWeight>(nameof(PanoseWeight), 2);
            Add<PanoseSpacing>(nameof(PanoseAspect), 3);
            Add<PanoseSymbolAspectRatio>("PanoseAspectRatioAndContrast", 4);
            Add<PanoseSymbolAspectRatio>("PanoseAspectRatio94", 5);
            Add<PanoseSymbolAspectRatio>("PanoseAspectRatio119", 6);
            Add<PanoseSymbolAspectRatio>("PanoseAspectRatio157", 7);
            Add<PanoseSymbolAspectRatio>("PanoseAspectRatio163", 8);
            Add<PanoseSymbolAspectRatio>("PanoseAspectRatio211", 9);
        }

        return values;
    }
}

public static class PanoseParser
{
    public static Panose Parse(DWriteProperties props)
    {
        byte[] panose = props.Panose;

        if (panose is null)
            return new Panose(PanoseFamily.Any, PanoseSerifStyle.ANY, props, false);

        // The contents of the Panose byte array depends on the value of the first byte. 
        // See https://docs.microsoft.com/en-us/windows/win32/api/dwrite_1/ns-dwrite_1-dwrite_panose 
        // for how the Family value changes the meaning of the following 9 bytes.
        PanoseFamily family = (PanoseFamily)panose[0];

        // Only fonts in TextDisplay family will identify their serif style
        PanoseSerifStyle style = PanoseSerifStyle.ANY;
        if (family == PanoseFamily.Text_Display)
            style = (PanoseSerifStyle)panose[1];

        // Warning - not all fonts store correct values for Panose information. 
        // If expanding PanoseParser in the future to read all values, direct casting
        // enums may lead to errors - safer parsing may be needed to take into account
        // faulty panose classifications.

        return new Panose(family, style, props);
    }
}
