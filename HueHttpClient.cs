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
            { "aliceblue", "F0F8FF" },
            { "aqua", "00FFFF" },
            { "aquamarine", "7FFFD4" },
            { "azure", "F0FFFF" },
            { "beige", "F5F5DC" },
            { "bisque", "FFE4C4" },
            { "black", "000000" },
            { "blue", "0000FF" },
            { "brown", "A52A2A" },
            { "burlyWood", "DEB887" },
            { "chartreuse", "7FFF00" },
            { "chocolate", "D2691E" },
            { "coral", "FF7F50" },
            { "cornsilk", "FFF8DC" },
            { "crimson", "DC143C" },
            { "cyan", "00FFFF" },
            { "gold", "FFD700" },
            { "gray", "808080" },
            { "green", "008000" },
            { "indigo", "4B0082" },
            { "ivory", "FFFFF0" },
            { "khaki", "F0E68C" },
            { "lavender", "E6E6FA" },
            { "lime", "00FF00" },
            { "limegreen", "32CD32" },
            { "linen", "FAF0E6" },
            { "magenta", "FF00FF" },
            { "maroon", "800000" },
            { "midnightblue", "191970" },
            { "mistyrose", "FFE4E1" },
            { "moccasin", "FFE4B5" },
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
    public void SetBridgeIp(string BridgeIp)
    {
        _bridgeIp = BridgeIp;
    }

    public bool ValidateBridgeIp()
    {
        return true;
    }
    public async Task DiscoverBridgeAsync()
{
    var localBridgeIp = await LoadBridgeIpFromConfigAsync();

    if (!string.IsNullOrEmpty(localBridgeIp))
    {
        _bridgeIp = localBridgeIp;
        Console.WriteLine($"Loaded bridge IP from config file: {localBridgeIp}");
    }
    else
    {
        await DiscoverAndSaveBridgeIpAsync();
    }
}

private async Task<string> LoadBridgeIpFromConfigAsync()
{
    var bridgeIpJson = await _JsonController.GetValueByKeyAsync("bridgeIp");
    return bridgeIpJson.GetValue<string>();
}

private async Task DiscoverAndSaveBridgeIpAsync()
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

        if (bridgeInfo.TryGetProperty("internalipaddress", out JsonElement internalIpAddress))
        {
            _bridgeIp = internalIpAddress.GetString();
            Console.WriteLine($"Discovered bridge IP: {_bridgeIp}");

            await SaveBridgeIpToConfigAsync(_bridgeIp);
            Console.WriteLine("Saved bridge IP to config file.");
        }
        else
        {
            throw new Exception("internalipaddress not found in the response.");
        }
    }
    else
    {
        throw new Exception("No bridges found in the response.");
    }
}

private async Task SaveBridgeIpToConfigAsync(string bridgeIp)
{
    await _JsonController.UpdateAsync(jsonNode =>
    {
        if (jsonNode is JsonObject jsonObject)
        {
            jsonObject["bridgeIp"] = bridgeIp;
        }
    });
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
            //Console.WriteLine($"App Key: {_appKey}");
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

    public async Task UpdateLightBrightnessLevelAsync(Guid lightId, double brightness)
    {
        var command = new UpdateLight().TurnOn().SetBrightness(brightness);
        await _hueClient.UpdateLightAsync(lightId, command);
    }
    public async Task SetLightColorAsync(Guid lightId, string color)
    {
        UpdateLight req;

        // Check if the color name exists in the dictionary and get the hex code
        if (colorHexCodes.TryGetValue(color, out string hexColor))
        {
            req = new UpdateLight().TurnOn().SetColor(new RGBColor(hexColor));
        }
        else
        {
            // Assume color is a hex code if not found in the dictionary
            req = new UpdateLight().TurnOn().SetColor(new RGBColor(color));
        }

        await _hueClient.UpdateLightAsync(lightId, req);
    }

    public async Task SetLightColorAsync(List<Guid> LightIds, string color)
    {
        foreach (var LightId in LightIds)
        {
        UpdateLight req;

        // Check if the color name exists in the dictionary and get the hex code
        if (colorHexCodes.TryGetValue(color, out string hexColor))
        {
            req = new UpdateLight().TurnOn().SetColor(new RGBColor(hexColor));
        }
        else
        {
            // Assume color is a hex code if not found in the dictionary
            req = new UpdateLight().TurnOn().SetColor(new RGBColor(color));
        }

        await _hueClient.UpdateLightAsync(LightId, req);
        }
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
