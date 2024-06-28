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
    private Dictionary<string, Guid> LightMap = [];
    private TaskCompletionSource<bool> _pollingTaskCompletionSource;
    public LocalHueApi _hueClient;
    private HttpClient _httpClient;
    private HueResponse<Light> _lights;
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
            //throw new Exception("Bridge IP not set. Please discover the bridge first.");
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
                Console.WriteLine($"Successfully registered with the Hue Bridge ({_bridgeIp}).\n");
                _lights = await GetLightsAsync();
                if (_lights.Data.Count != 0)
                {
                    Console.WriteLine("Found devices:");
                    _lights.Data.ForEach(light =>
                    {
                        Console.WriteLine($"Name: {light.Metadata.Name} - Type: {light.Type}\n");
                        LightMap.Add(light.Metadata.Name, light.Id);
                    });
                }
                else
                {
                    Console.WriteLine("Couldn't find any lamps!");
                }
            }
            else
            {
                Console.WriteLine("Waiting for the link button to be pressed...\n");
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

    public async Task SetLightColorAsync(string lamp, string color)
    {
        var LampName = lamp == "left" ? "room_streaming_left_lamp" : "room_streaming_right_lamp";
        LightMap.TryGetValue(LampName, out var LightGuid);
        UpdateLight req;
        req = new UpdateLight().TurnOn().SetColor(new RGBColor(color));
        await _hueClient.UpdateLightAsync(LightGuid, req);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
