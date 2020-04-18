namespace CharacterMap.Models
{
    public partial struct UnicodeRange
    {
        public uint Start { get; }
        public uint End { get; }

        public UnicodeRange(uint start, uint end)
        {
            Start = start;
            End = end;
        }
    }

    public partial struct UnicodeRange
    {
        public static UnicodeRange Arabic                   { get; } = new UnicodeRange(0x0600, 0x06FF);
        public static UnicodeRange Cyrillic                 { get; } = new UnicodeRange(0x0400, 0x052F);
        public static UnicodeRange Thai                     { get; } = new UnicodeRange(0x0E00, 0x0E7F);
        public static UnicodeRange GreekAndCoptic           { get; } = new UnicodeRange(0x0370, 0x03FF);
        public static UnicodeRange Hebrew                   { get; } = new UnicodeRange(0x0590, 0x05FF);
        public static UnicodeRange CJKUnifiedIdeographs     { get; } = new UnicodeRange(0x4E00, 0x9FFF);


        public static UnicodeRange Dingbats                 { get; } = new UnicodeRange(0x2700, 0x27BF);
        public static UnicodeRange Emoticons                { get; } = new UnicodeRange(0x1F600, 0x1F64F);

        public static UnicodeRange MiscSymbols              { get; } = new UnicodeRange(0x1F300, 0x1F5FF);
        public static UnicodeRange SupplementalSymbols      { get; } = new UnicodeRange(0x1F900, 0x1F9FF);
        public static UnicodeRange SymbolsExtended          { get; } = new UnicodeRange(0x1FA70, 0x1FAFF);
        public static UnicodeRange TransportSymbols         { get; } = new UnicodeRange(0x1F680, 0x1F6FF);
    }
}
