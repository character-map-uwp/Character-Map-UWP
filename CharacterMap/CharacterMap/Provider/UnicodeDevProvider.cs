namespace CharacterMap.Provider;

public class UnicodeDevProvider : DevProviderBase
{
    public UnicodeDevProvider(CharacterRenderingOptions o, Character c) : base(o, c)
    {
        DisplayName = "Unicode";
    }

    private static List<DevOption> _allOptions { get; } =
    [
        new("TxtUniCodepoint/Header", null),
        new("TxtUniHexValue/Text", null),
        new("TxtUTF16/Header", null),
        new("TxtGlyphIndex/Header", null)
    ];

    protected override DevProviderType GetDevProviderType() => DevProviderType.Unicode;
    protected override IReadOnlyList<DevOption> OnGetContextOptions() => Inflate();
    protected override IReadOnlyList<DevOption> OnGetOptions() => Inflate();
    public override IReadOnlyList<DevOption> GetAllOptions() => _allOptions;

    IReadOnlyList<DevOption> Inflate()
    {
        CMFontFace v = Options.Variant;
        Character c = Character;

        string hex = c.UnicodeIndex.ToString("x4").ToUpper();
        string utf = null;

        if (Unicode.RequiresSurrogates(c))
        {
            Windows.Data.Text.UnicodeCharacters.GetSurrogatePairFromCodepoint(c.UnicodeIndex, out char high, out char low);
            utf = @$"\u{(uint)high:x4}\u{(uint)low:x4}";
        }
        else
            utf = @$"\u{(uint)c.Char[0]:x4}";

        string glyphStr = null;
        if (c is GlyphCharacter gc)
            glyphStr = $"{gc.GlyphIndex}";
        else if (Options.Analysis?.GlyphIndices is { Count: > 0 } gIndices)
            glyphStr = string.Join(", ", gIndices);
        else if (Options.Analysis?.Indicies is { Length: > 0 } rIndices)
            glyphStr = string.Join(", ", rIndices.SelectMany(r => r));

        List<DevOption> ops =
        [
            new("TxtUniCodepoint/Header", $"{c.UnicodeIndex}"),
            new("TxtUniHexValue/Text", c.UnicodeString),
            new("TxtUTF16/Header", $"{utf}"),
            new("TxtGlyphIndex/Header", glyphStr),
        ];

        return ops;
    }
}
