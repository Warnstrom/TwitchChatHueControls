
public static class HexColorMapDictionary
{
    static Dictionary<string, string> HexColorMap = new Dictionary<string, string>()
        {
            { "aliceblue", "F0F8FF" },
            { "aqua", "00FFFF" },
            { "aquamarine", "7FFFD4" },
            { "bisque", "FFE4C4" },
            { "black", "000000" },
            { "blue", "0000FF" },
            { "chartreuse", "7FFF00" },
            { "coral", "FF7F50" },
            { "crimson", "DC143C" },
            { "cyan", "00FFFF" },
            { "gold", "FFD700" },
            { "gray", "808080" },
            { "green", "008000" },
            { "indigo", "4B0082" },
            { "khaki", "F0E68C" },
            { "lavender", "E6E6FA" },
            { "lime", "00FF00" },
            { "limegreen", "32CD32" },
            { "linen", "FAF0E6" },
            { "magenta", "FF00FF" },
            { "maroon", "800000" },
            { "midnightblue", "191970" },
            { "mistyrose", "FFE4E1" },
            { "navy", "000080" },
            { "olive", "808000" },
            { "orange", "FFA500" },
            { "orchid", "DA70D6" },
            { "pink", "FFC0CB" },
            { "flamingo", "FC8EAC" },
            { "plum", "DDA0DD" },
            { "purple", "800080" },
            { "red", "FF0000" },
            { "salmon", "FA8072" },
            { "silver", "C0C0C0" },
            { "snow", "FFFAFA" },
            { "tan", "D2B48C" },
            { "teal", "008080" },
            { "thistle", "D8BFD8" },
            { "tomato", "FF6347" },
            { "turquoise", "40E0D0" }, // Wrong
            { "violet", "EE82EE" }, // Correct
            { "white", "FFFFFF" },
            { "yellow", "FFFF00" },
        };

    static HexColorMapDictionary()
    {

    }

    public static string? Get(string key)
    {
        HexColorMap.TryGetValue(key, out string value);
        if (value != null) {
            return value;
        } else {
            return null;
        }
    }

    public static Dictionary<string, string> GetAllColors()
    {
        return HexColorMap;
    }
}
