using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public class JsonFileController
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonFileController(string filePath)
    {
        _filePath = filePath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true, // Format the JSON to be more readable
        };
    }

    // Reads the JSON file and deserializes it into a JsonNode
    public async Task<JsonNode?> ReadAsync()
    {
        if (!File.Exists(_filePath))
        {
            // Create the file with default JSON content
            var defaultJson = new
            {
                bridgeIp = "",
                AccessToken = "",
                RefreshToken = "",
                AppKey = ""
            };

            string jsonString = JsonSerializer.Serialize(defaultJson, _jsonOptions);
            await File.WriteAllTextAsync(_filePath, jsonString);
        }

        using (FileStream fs = File.OpenRead(_filePath))
        {
            return await JsonSerializer.DeserializeAsync<JsonNode>(fs, _jsonOptions);
        }
    }

    // Writes the JsonNode to the JSON file
    public async Task WriteAsync(JsonNode data)
    {
        using (FileStream fs = File.Create(_filePath))
        {
            await JsonSerializer.SerializeAsync(fs, data, _jsonOptions);
        }
    }

    // Updates the JSON file by applying the updateAction to the current JsonNode data
    public async Task UpdateAsync(Action<JsonNode?> updateAction)
    {
        JsonNode? data = await ReadAsync() ?? new JsonObject();
        updateAction(data);
        await WriteAsync(data);
    }

        public async Task<JsonNode?> GetValueByKeyAsync(string key)
    {
        JsonNode? jsonNode = await ReadAsync();

        if (jsonNode is JsonObject jsonObject && jsonObject.TryGetPropertyValue(key, out JsonNode? value))
        {
            return value;
        }

        return null;
    }

        public async Task<Dictionary<string, string>> ReadAsDictionaryAsync()
    {
        JsonNode? jsonNode = await ReadAsync();

        Dictionary<string, string> dictionary = new Dictionary<string, string>();

        if (jsonNode is JsonObject jsonObject)
        {
            foreach (var kvp in jsonObject)
            {
                if (kvp.Value is JsonValue value && value.TryGetValue(out string? stringValue))
                {
                    dictionary[kvp.Key] = stringValue;
                }
            }
        }

        return dictionary;
    }
}
