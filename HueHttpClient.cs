using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using HueApi;
using HueApi.Models;
using HueApi.Models.Requests;
using HueApi.ColorConverters;
using HueApi.ColorConverters.Original.Extensions;
public class HueController : IDisposable
{
          Dictionary<string, string> colorHexCodes = new Dictionary<string, string>()
        {
            { "AliceBlue", "F0F8FF" },
            { "AntiqueWhite", "FAEBD7" },
            { "Aqua", "00FFFF" },
            { "Aquamarine", "7FFFD4" },
            { "Azure", "F0FFFF" },
            { "Beige", "F5F5DC" },
            { "Bisque", "FFE4C4" },
            { "Black", "000000" },
            { "BlanchedAlmond", "FFEBCD" },
            { "Blue", "0000FF" },
            { "BlueViolet", "8A2BE2" },
            { "Brown", "A52A2A" },
            { "BurlyWood", "DEB887" },
            { "CadetBlue", "5F9EA0" },
            { "Chartreuse", "7FFF00" },
            { "Chocolate", "D2691E" },
            { "Coral", "FF7F50" },
            { "CornflowerBlue", "6495ED" },
            { "Cornsilk", "FFF8DC" },
            { "Crimson", "DC143C" },
            { "Cyan", "00FFFF" },
            { "DarkBlue", "00008B" },
            { "DarkCyan", "008B8B" },
            { "DarkGoldenRod", "B8860B" },
            { "DarkGray", "A9A9A9" },
            { "DarkGreen", "006400" },
            { "DarkKhaki", "BDB76B" },
            { "DarkMagenta", "8B008B" },
            { "DarkOliveGreen", "556B2F" },
            { "DarkOrange", "FF8C00" },
            { "DarkOrchid", "9932CC" },
            { "DarkRed", "8B0000" },
            { "DarkSalmon", "E9967A" },
            { "DarkSeaGreen", "8FBC8F" },
            { "DarkSlateBlue", "483D8B" },
            { "DarkSlateGray", "2F4F4F" },
            { "DarkTurquoise", "00CED1" },
            { "DarkViolet", "9400D3" },
            { "DeepPink", "FF1493" },
            { "DeepSkyBlue", "00BFFF" },
            { "DimGray", "696969" },
            { "DodgerBlue", "1E90FF" },
            { "FireBrick", "B22222" },
            { "FloralWhite", "FFFAF0" },
            { "ForestGreen", "228B22" },
            { "Fuchsia", "FF00FF" },
            { "Gainsboro", "DCDCDC" },
            { "GhostWhite", "F8F8FF" },
            { "Gold", "FFD700" },
            { "GoldenRod", "DAA520" },
            { "Gray", "808080" },
            { "Green", "008000" },
            { "GreenYellow", "ADFF2F" },
            { "HoneyDew", "F0FFF0" },
            { "HotPink", "FF69B4" },
            { "IndianRed", "CD5C5C" },
            { "Indigo", "4B0082" },
            { "Ivory", "FFFFF0" },
            { "Khaki", "F0E68C" },
            { "Lavender", "E6E6FA" },
            { "LavenderBlush", "FFF0F5" },
            { "LawnGreen", "7CFC00" },
            { "LemonChiffon", "FFFACD" },
            { "LightBlue", "ADD8E6" },
            { "LightCoral", "F08080" },
            { "LightCyan", "E0FFFF" },
            { "LightGoldenRodYellow", "FAFAD2" },
            { "LightGray", "D3D3D3" },
            { "LightGreen", "90EE90" },
            { "LightPink", "FFB6C1" },
            { "LightSalmon", "FFA07A" },
            { "LightSeaGreen", "20B2AA" },
            { "LightSkyBlue", "87CEFA" },
            { "LightSlateGray", "778899" },
            { "LightSteelBlue", "B0C4DE" },
            { "LightYellow", "FFFFE0" },
            { "Lime", "00FF00" },
            { "LimeGreen", "32CD32" },
            { "Linen", "FAF0E6" },
            { "Magenta", "FF00FF" },
            { "Maroon", "800000" },
            { "MediumAquaMarine", "66CDAA" },
            { "MediumBlue", "0000CD" },
            { "MediumOrchid", "BA55D3" },
            { "MediumPurple", "9370DB" },
            { "MediumSeaGreen", "3CB371" },
            { "MediumSlateBlue", "7B68EE" },
            { "MediumSpringGreen", "00FA9A" },
            { "MediumTurquoise", "48D1CC" },
            { "MediumVioletRed", "C71585" },
            { "MidnightBlue", "191970" },
            { "MintCream", "F5FFFA" },
            { "MistyRose", "FFE4E1" },
            { "Moccasin", "FFE4B5" },
            { "NavajoWhite", "FFDEAD" },
            { "Navy", "000080" },
            { "OldLace", "FDF5E6" },
            { "Olive", "808000" },
            { "OliveDrab", "6B8E23" },
            { "Orange", "FFA500" },
            { "OrangeRed", "FF4500" },
            { "Orchid", "DA70D6" },
            { "PaleGoldenRod", "EEE8AA" },
            { "PaleGreen", "98FB98" },
            { "PaleTurquoise", "AFEEEE" },
            { "PaleVioletRed", "DB7093" },
            { "PapayaWhip", "FFEFD5" },
            { "PeachPuff", "FFDAB9" },
            { "Peru", "CD853F" },
            { "Pink", "FFC0CB" },
            { "Flamingo", "FC8EAC" },
            { "Plum", "DDA0DD" },
            { "PowderBlue", "B0E0E6" },
            { "Purple", "800080" },
            { "Red", "FF0000" },
            { "RosyBrown", "BC8F8F" },
            { "RoyalBlue", "4169E1" },
            { "SaddleBrown", "8B4513" },
            { "Salmon", "FA8072" },
            { "SandyBrown", "F4A460" },
            { "SeaGreen", "2E8B57" },
            { "SeaShell", "FFF5EE" },
            { "Sienna", "A0522D" },
            { "Silver", "C0C0C0" },
            { "SkyBlue", "87CEEB" },
            { "SlateBlue", "6A5ACD" },
            { "SlateGray", "708090" },
            { "Snow", "FFFAFA" },
            { "SpringGreen", "00FF7F" },
            { "SteelBlue", "4682B4" },
            { "Tan", "D2B48C" },
            { "Teal", "008080" },
            { "Thistle", "D8BFD8" },
            { "Tomato", "FF6347" },
            { "Turquoise", "40E0D0" },
            { "Violet", "EE82EE" },
            { "Wheat", "F5DEB3" },
            { "White", "FFFFFF" },
            { "WhiteSmoke", "F5F5F5" },
            { "Yellow", "FFFF00" },
            { "YellowGreen", "9ACD32" }
        };
    private TaskCompletionSource<bool> _pollingTaskCompletionSource;
    public LocalHueApi _hueClient;
    private HttpClient _httpClient;
    private JsonFileController _JsonController;
    private string _appKey;
    private string _bridgeIp;
    private Timer _pollingTimer;
    private const int PollingInterval = 10000;
    private bool _isPolling;

    public HueController(JsonFileController JsonController)
    {
        _httpClient = new HttpClient();
        _JsonController = JsonController;
    }
    public void SetBridgeIp(string BridgeIp) {
        _bridgeIp = BridgeIp;
    }

    public bool ValidateBridgeIp() {
        return true;
    }
    public async Task DiscoverBridgeAsync()
    {
        var bridgeIpJson = await _JsonController.GetValueByKeyAsync("bridgeIp");
        var localBridgeIp = bridgeIpJson.GetValue<string>();

        if (!string.IsNullOrEmpty(localBridgeIp))
        {
            _bridgeIp = localBridgeIp;
            Console.WriteLine($"Loaded bridge IP from config file: {localBridgeIp}");
        }
        else
        {
            Console.WriteLine("Couldn't find bridge IP from config file");
            Console.WriteLine("Fetching new Bridge IP from discovery.meethue.com/");

            var response = await _httpClient.GetAsync("https://discovery.meethue.com/");
            response.EnsureSuccessStatusCode();

            var bridges = await response.Content.ReadAsStringAsync();
            var responseJson = JsonDocument.Parse(bridges);

            if (responseJson.RootElement.ValueKind == JsonValueKind.Array && responseJson.RootElement.GetArrayLength() > 0)
            {
                var bridgeInfo = responseJson.RootElement[0];

                if (bridgeInfo.TryGetProperty("internalipaddress", out JsonElement internalipaddress))
                {
                    _bridgeIp = internalipaddress.GetString();
                    Console.WriteLine($"Discovered bridge IP: {_bridgeIp}");

                    await _JsonController.UpdateAsync(jsonNode =>
                    {
                        if (jsonNode is JsonObject jsonObject)
                        {
                            jsonObject["bridgeIp"] = _bridgeIp;
                        }
                    });
                    Console.WriteLine("Saved bridge IP to config file.");
                }
                else
                {
                    throw new Exception("internalipaddress not found in the response.");
                }
            }
        }
    }

    public async Task<bool> TryRegisterApplicationAsync(string appName, string deviceName)
    {
        if (string.IsNullOrEmpty(_bridgeIp))
        {
            throw new Exception("Bridge IP not set. Please discover the bridge first.");
        }

        string url = $"http://{_bridgeIp}/api";
        var payload = new
        {
            devicetype = $"{appName}#{deviceName}",
            generateclientkey = true
        };


        var response = await _httpClient.PostAsJsonAsync(url, payload);
        var responseContent = await response.Content.ReadAsStringAsync();
        var responseJson = JsonDocument.Parse(responseContent);

        var result = responseJson.RootElement[0];

        if (result.TryGetProperty("success", out JsonElement success))
        {
            _appKey = success.GetProperty("username").GetString();
            await _JsonController.UpdateAsync(jsonNode =>
            {
                if (jsonNode is JsonObject jsonObject)
                {
                    jsonObject["AppKey"] = _appKey;
                }
            });
            Console.WriteLine($"App Key: {_appKey}");
            return true;
        }
        else if (result.TryGetProperty("error", out JsonElement error))
        {
            Console.WriteLine(error.GetProperty("description").GetString());
            return false;
        }

        return false;
    }

public async Task<bool> StartPollingForLinkButton(string appName, string deviceName)
{
    _pollingTaskCompletionSource = new TaskCompletionSource<bool>();
    _isPolling = true;

    _pollingTimer = new Timer(async _ =>
    {
        bool registered = await TryRegisterApplicationAsync(appName, deviceName);
        if (registered)
        {
            _pollingTimer?.Change(Timeout.Infinite, 0);
            _isPolling = false;
            _pollingTaskCompletionSource.SetResult(true);
            _hueClient = new LocalHueApi(_bridgeIp, _appKey);
            Console.WriteLine("Successfully registered with the Hue Bridge.");
        }
        else
        {
            Console.WriteLine("Waiting for the link button to be pressed...");
        }
    }, null, 0, PollingInterval);

    return await _pollingTaskCompletionSource.Task;
}

    public async Task<HueResponse<Light>> GetLightsAsync()
    {
        return await _hueClient.GetLightsAsync();
    }

    public async Task TurnOnLightAsync(Guid lightId)
    {
        var command = new UpdateLight().TurnOn();
        await _hueClient.UpdateLightAsync(lightId, command);
    }

    public async Task TurnOffLightAsync(Guid lightId)
    {
        var command = new UpdateLight().TurnOff();
        await _hueClient.UpdateLightAsync(lightId, command);
    }

public async Task SetLightColorAsync(Guid lightId, string color)
{
    UpdateLight req;
    string hexColor;

    // Check if the color name exists in the dictionary and get the hex code
    if (colorHexCodes.TryGetValue(color, out hexColor))
    {
        req = new UpdateLight().TurnOn().SetColor(new RGBColor(hexColor));
    }
    else
    {
        // Assume color is a hex code if not found in the dictionary
        req = new UpdateLight().TurnOn().SetColor(new RGBColor(color));
    }

    var result = await _hueClient.UpdateLightAsync(lightId, req);
}

    public async Task SetLightBrightnessAsync(Guid lightId, byte brightness)
    {
        var command = new UpdateLight().SetBrightness(brightness);
        await _hueClient.UpdateLightAsync(lightId, command);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
