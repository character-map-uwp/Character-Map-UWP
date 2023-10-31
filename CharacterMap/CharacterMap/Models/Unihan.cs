// Ignore Spelling: Unihan

using SQLite;
using System.Diagnostics;

namespace CharacterMap.Models;

/* 
 * These are not in the order they are listed in the Unihan Readings 
 * file but in the order we would like to sort them in the app.
 * If the order is changed the database must be rebuilt
 */
public enum UnihanFieldType : int
{
    Definition = 0,
    Mandarin = 1,
    Cantonese = 2,
    Japanese = 3,
    JapaneseKun = 4,
    JapaneseOn = 5,
    Korean = 6,
    Hangul = 7,
    Vietnamese = 8,
    Tang = 9,
    HanyuPinlu = 10,
    HanyuPinyin = 11,
    TGHZ2013 = 12,
    XHC1983 = 13,
    SMSZD2003Readings = 14,
}

public class UnihanData
{
    public UnihanReading Definition { get; }

    public IReadOnlyList<UnihanReading> Pronunciations { get; }

    public UnihanData(List<UnihanReading> readings)
    {
        Definition      = readings.Where(r => r.Type == UnihanFieldType.Definition && !string.IsNullOrEmpty(r.Description)).FirstOrDefault();
        Pronunciations  = readings.Where(r => r.Type != UnihanFieldType.Definition).OrderBy(r => r.Type).ToList();
    }
}

public class UnihanFieldData
{
    private static Dictionary<UnihanFieldType, UnihanFieldData> _dic { get; } = new();
    public static UnihanFieldData Get(UnihanFieldType type)
    {
        if (!_dic.TryGetValue(type, out UnihanFieldData value))
            _dic[type] = value = new UnihanFieldData(type);

        return value;
    }

    private UnihanFieldData(UnihanFieldType type)
    {
        Type = type;
        Name = Localization.Get($"UnihanTypeName{type}"); 
        Description = Localization.Get($"UnihanTypeDescription{type}");
    }

    public string Name { get; }
    public string Description { get; }
    public UnihanFieldType Type { get; }
}


[DebuggerDisplay("{Type} {Description}")]
public class UnihanReading
{
    public UnihanReading(int index, UnihanFieldType type, string description)
    {
        Index = index;
        Type = type;
        Description = description;
    }

#if DEBUG
    [Indexed, Column("Ix")]
#endif
    public int Index { get; set; }

#if DEBUG
    [Column(nameof(Type))]
#endif
    public UnihanFieldType Type { get; set; }

#if DEBUG
    [Column(nameof(Description))]
#endif
    public string Description { get; set; }

    private UnihanFieldData _field = null;
    public UnihanFieldData Field => _field ??= UnihanFieldData.Get(Type);
}
