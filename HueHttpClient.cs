using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using HueApi;
using HueApi.Models;
using HueApi.Models.Requests;

public class HueController
{
    private LocalHueApi _hueClient;
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

    public void StartPollingForLinkButton(string appName, string deviceName)
    {
        _isPolling = true;
        _pollingTimer = new Timer(async _ =>
        {
            bool registered = await TryRegisterApplicationAsync(appName, deviceName);
            if (registered)
            {
                _pollingTimer?.Change(Timeout.Infinite, 0);
                _isPolling = false;

                Console.WriteLine("Successfully registered with the Hue Bridge.");
            }
            else
            {
                Console.WriteLine("Waiting for the link button to be pressed...");
            }
        }, null, 0, PollingInterval);
        _hueClient = new LocalHueApi(_bridgeIp, _appKey);

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
        Console.WriteLine("TURN OFF ");
        var command = new UpdateLight().TurnOff();
        await _hueClient.UpdateLightAsync(lightId, command);
        Console.WriteLine("TURNED OFF ");

    }

    public async Task SetLightColorAsync(Guid lightId, double x, double y)
    {
        var command = new UpdateLight().SetColor(x, y);
        await _hueClient.UpdateLightAsync(lightId, command);
    }

    public async Task SetLightBrightnessAsync(Guid lightId, byte brightness)
    {
        var command = new UpdateLight().SetBrightness(brightness);
        await _hueClient.UpdateLightAsync(lightId, command);
    }
}
