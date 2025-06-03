﻿using System.Collections.ObjectModel;

namespace CharacterMap.Models;

public interface IFontCollection
{
    long Id { get; set; }

    string Name { get; set; }

    string GetFlatArgs();

    bool ContainsFamily(string fontName);

    string Icon { get; }

    IReadOnlyList<CMFontFamily> GetFontFamilies();
}

public class SQLiteFontCollection
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string Fonts { get; set; }

    public UserFontCollection AsUserFontCollection()
    {
        return new UserFontCollection
        {
            Id = Id,
            Name = Name,
            Fonts = new(Fonts.Split(''))
        };
    }
}

public class SQLiteSmartFontCollection
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string Filters { get; set; }

    public SmartFontCollection AsSmartFontCollection()
    {
        return new SmartFontCollection
        {
            Id = Id,
            Name = Name,
            Filters = Filters.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).ToList()
        };
    }
}

[DebuggerDisplay("({Id}) Name: {Name}, {Fonts.Count} Fonts")]
public class UserFontCollection : IFontCollection
{
    public bool IsSystemSymbolCollection { get; set; }
    public long Id { get; set; }
    public string Name { get; set; }
    public HashSet<string> Fonts { get; set; } = new();

    public string GetFlatArgs() => string.Join('', Fonts);

    public bool ContainsFamily(string fontName) => Fonts.Contains(fontName);

    public string Icon => null;

    public IReadOnlyList<CMFontFamily> GetFontFamilies() =>
        FontFinder.Fonts.Where(f => Fonts.Contains(f.Name)).ToList();
}

[DebuggerDisplay("({Id}) Name: {Name}, {Filters.Count} Filters")]
public class SmartFontCollection : IFontCollection
{
    static string ICON { get; } = new ('\uE945', 1);

    public long Id { get; set; }
    public string Name { get; set; }
    public List<string> Filters { get; set; }
    public HashSet<string> FontNames { get; set; }

    public IReadOnlyList<CMFontFamily> Fonts { get; private set; } = [];

    public IReadOnlyList<CMFontFamily> UpdateFonts()
    { 
        var fonts = FontFinder.Fonts.AsEnumerable();

        foreach (var filter in Filters)
            fonts = FontFinder.QueryFontList(filter, fonts, null).FontList;

        Fonts = fonts.ToList();
        FontNames = Fonts.Select(f => f.Name).ToHashSet();
        return Fonts;
    }

    public IReadOnlyList<CMFontFamily> GetFontFamilies() => UpdateFonts();

    public string GetFlatArgs() => string.Join("\r\n", Filters);

    public bool ContainsFamily(string fontName)
    {
        if (FontNames is null)
            UpdateFonts();

        return FontNames.Contains(fontName);
    }

    public string Icon => ICON;
}