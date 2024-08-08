using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using HueApi.BridgeLocator;
using HueApi;
using HueApi.Models;
using HueApi.Models.Requests;
using HueApi.ColorConverters;
using HueApi.ColorConverters.Original.Extensions;
using Spectre.Console;

public interface IHueController : IDisposable
{
    Task DiscoverBridgeAsync();
    Task<bool> TryRegisterApplicationAsync(string appName, string deviceName);
    Task<bool> StartPollingForLinkButtonAsync(string appName, string deviceName, string bridgeIp, string appKey);
    void GetLightsAsync();
    Task SetLightColorAsync(string lamp, string color);
}

public class HueController : IHueController, IDisposable
{
    private readonly HttpBridgeLocator locator = new();
    private readonly Dictionary<string, Guid> _lightMap = new();
    private readonly HttpClient _httpClient;
    private readonly IJsonFileController _jsonController;
    private TaskCompletionSource<bool> _pollingTaskCompletionSource;
    private LocalHueApi _hueClient;
    private HueResponse<Light> _lights;
    private string _appKey;
    private string _bridgeIp;
    private string _bridgeId;
    private Timer _pollingTimer;
    private bool _isPolling;

    public HueController(IJsonFileController jsonController)
    {
        _httpClient = new HttpClient();
        _jsonController = jsonController;
    }
    public async Task DiscoverBridgeAsync()
    {
        // Potential useful way to discover bridges????
        //var Bridges = await HueBridgeDiscovery.CompleteDiscoveryAsync(new TimeSpan(5), new TimeSpan(30));

        IEnumerable<LocatedBridge> bridgeIPs = await locator.LocateBridgesAsync(TimeSpan.FromSeconds(10));
        if (bridgeIPs.Any())
        {
            var LocatedBridge = bridgeIPs.First();
            Console.WriteLine(LocatedBridge);
            _bridgeIp = LocatedBridge.IpAddress;
            await SaveConfigAsync("bridgeIp", _bridgeIp);

            _bridgeId = LocatedBridge.BridgeId;
            await SaveConfigAsync("bridgeId", _bridgeId);
        }
        else
        {
            Console.WriteLine("No Bridges found.");
            Console.WriteLine(bridgeIPs);
        }
    }

    private async Task SaveConfigAsync(string key, string value)
    {
        await _jsonController.UpdateAsync(jsonNode =>
        {
            if (jsonNode is JsonObject jsonObject)
            {
                jsonObject[key] = value;
            }
        });
    }

    public async Task<bool> TryRegisterApplicationAsync(string appName, string deviceName)
    {
        if (string.IsNullOrEmpty(_bridgeIp))
        {
            await DiscoverBridgeAsync();
        }

        string url = $"http://{_bridgeIp}/api";
        var payload = new { devicetype = $"{appName}#{deviceName}", generateclientkey = true };

        var response = await _httpClient.PostAsJsonAsync(url, payload);
        var responseContent = await response.Content.ReadAsStringAsync();
        var responseJson = JsonDocument.Parse(responseContent);

        var result = responseJson.RootElement[0];

        if (result.TryGetProperty("success", out var success))
        {
            _appKey = success.GetProperty("username").GetString();
            await _jsonController.UpdateAsync(jsonNode =>
            {
                if (jsonNode is JsonObject jsonObject)
                {
                    jsonObject["AppKey"] = _appKey;
                }
            });
            return true;
        }
        else if (result.TryGetProperty("error", out var error))
        {
            Console.WriteLine(error.GetProperty("description").GetString());
            return false;
        }

        return false;
    }

    public async Task<bool> StartPollingForLinkButtonAsync(string appName, string deviceName, string bridgeIp, string appKey)
    {
        _pollingTaskCompletionSource = new TaskCompletionSource<bool>();
        if (string.IsNullOrEmpty(appKey))
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
                    AnsiConsole.MarkupLine($"[bold green]Successfully registered with the Hue Bridge[/] [bold yellow]({_bridgeIp})[/]\n");
                    GetLightsAsync();
                    _pollingTimer.Dispose();
                }
                else
                {
                    AnsiConsole.MarkupLine("[bold yellow]Waiting for the link button to be pressed...[/]\n");
                }
            }, null, 0, 5000);
        }
        else
        {
            _hueClient = new LocalHueApi(bridgeIp, appKey);
            AnsiConsole.MarkupLine($"[bold green]Successfully connected with Hue Bridge using predefined ip[/] [bold yellow]({bridgeIp})[/]\n");
            GetLightsAsync();
            _pollingTaskCompletionSource.SetResult(true);

        }
        return await _pollingTaskCompletionSource.Task;
    }

    public async void GetLightsAsync()
    {
        _lights = await _hueClient.GetLightsAsync();
        if (_lights.Data.Count != 0)
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Spectre.Console.Color.Teal);

            table.AddColumn("Type");
            table.AddColumn(new TableColumn("Name").Centered());

            _lights.Data.ForEach(light =>
            {
                table.AddRow(light.Type, light.Metadata.Name);
                _lightMap[light.Metadata.Name] = light.Id;
            });
            //AnsiConsole.Write(table);
        }
        else
        {
            Console.WriteLine("Couldn't find any lamps!");
        }
    }

    public async Task SetLightColorAsync(string lamp, string color)
    {
        string lampName = GetLampName(lamp);
        if (lampName == null)
        {
            Console.WriteLine($"Invalid lamp identifier: {lamp}");
            return;
        }

        if (_lightMap.TryGetValue(lampName, out var lightGuid))
        {
            var command = new UpdateLight().SetColor(new RGBColor(color));
            await _hueClient.UpdateLightAsync(lightGuid, command);
        }
        else
        {
            Console.WriteLine($"Lamp {lampName} not found.");
        }
    }

    private static string? GetLampName(string lamp)
    {
        return lamp switch
        {
            "left" => "room_streaming_left_lamp",
            "right" => "room_streaming_right_lamp",
            _ => null,
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _pollingTimer?.Dispose();
    }
}