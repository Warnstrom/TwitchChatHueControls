
public static class HexColorMapDictionary
{
    private static readonly JsonFileController jsonController = new("colors.json");

    static Dictionary<string, string> HexColorMap = new Dictionary<string, string>();

    static HexColorMapDictionary()
    {
        LoadColors();
    }

    private static async void LoadColors() 
    {
        HexColorMap = await jsonController.ReadAsDictionaryAsync();
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
