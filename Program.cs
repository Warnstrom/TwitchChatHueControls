using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;
using DotNetEnv;
using Microsoft.Extensions.Configuration;

namespace TwitchChatHueControls
{
    class Program
    {
        private static async Task Main(string[] args)
        {
            // Create a new ServiceCollection
            var serviceCollection = new ServiceCollection();

            // Configure services
            ConfigureServices(serviceCollection);

            // Build the ServiceProvider
            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Run the application
            await serviceProvider.GetRequiredService<App>().RunAsync();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Load environment variables from .env file
            Env.Load();

            // Create and configure the ConfigurationBuilder
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            // Build the configuration
            IConfiguration configuration = configurationBuilder.Build();

            // Register IConfiguration instance
            services.AddSingleton<IConfiguration>(configuration);
            // Register services
            services.AddSingleton<IJsonFileController, JsonFileController>(sp => new JsonFileController("appsettings.json"));
            services.AddSingleton<IHueController, HueController>();
            services.AddSingleton<TwitchLib.Api.TwitchAPI>();

            // Register the main application entry point
            services.AddTransient<App>();
        }
    }

    public class App
    {
        private readonly IConfiguration _configuration;
        private readonly IJsonFileController _jsonController;
        private readonly IHueController _hueController;
        private readonly TwitchLib.Api.TwitchAPI _api;

        public App(IConfiguration configuration, IJsonFileController jsonController, IHueController hueController, TwitchLib.Api.TwitchAPI api)
        {
            _configuration = configuration;
            _jsonController = jsonController;
            _hueController = hueController;
            _api = api;
        }

        public async Task RunAsync()
        {
            await StartMenu();
        }

        private async Task StartMenu()
        {
            while (true)
            {
                await RenderStartMenu();
                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await ConfigureTwitchTokens();
                        break;
                    case "2":
                        await StartApp();
                        break;
                    default:
                        Console.WriteLine("Invalid choice. Please select again.");
                        break;
                }
            }
        }

        private async Task RenderStartMenu()
        {
            bool twitchConfigured = await ValidateTwitchConfiguration(_api);
            await ValidateHueConfiguration();

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("╔══════════════════════════════════════╗");
            Console.WriteLine("║           Welcome To Yuki's          ║");
            Console.WriteLine("║            Disco Lights              ║");
            Console.WriteLine("╠══════════════════════════════════════╣");
            string formattedText = GetConfiguredSymbol(twitchConfigured) == "Complete" ? $"║ 1. Connect to Twitch ({GetConfiguredSymbol(twitchConfigured)})      ║" : $"║ 1. Connect to Twitch ({GetConfiguredSymbol(twitchConfigured)})    ║";
            Console.WriteLine(formattedText);
            Console.WriteLine("║ 2. Start Bot                         ║");
            Console.WriteLine("╚══════════════════════════════════════╝");
            Console.ResetColor();
        }

        private async Task ValidateHueConfiguration()
        {
            BridgeValidator validator = new();

            string localBridgeIp = _configuration["bridgeIp"];
            string localBridgeId = _configuration["bridgeId"];;
            string localAppKey = _configuration["AppKey"];

            if (string.IsNullOrEmpty(localBridgeIp) || string.IsNullOrEmpty(localBridgeId))
            {
                await _hueController.DiscoverBridgeAsync();
                localBridgeIp = await _jsonController.GetValueByKeyAsync<string>("bridgeIp");
                localBridgeId = await _jsonController.GetValueByKeyAsync<string>("bridgeId");
            }

            if (!File.Exists("huebridge_cacert.pem"))
            {
                if (!string.IsNullOrEmpty(localBridgeIp))
                {
                    await CertificateService.ConfigureCertificate(new[] { localBridgeIp, "443", "huebridge_cacert.pem" });
                }
                else
                {
                    Console.WriteLine("Bridge IP is missing, cannot configure the certificate.");
                }
            }
        }

        private async Task<bool> ValidateTwitchConfiguration(TwitchLib.Api.TwitchAPI api)
        {
            string accessToken = await _jsonController.GetValueByKeyAsync<string>("AccessToken");

            if (!string.IsNullOrEmpty(accessToken) && await api.Auth.ValidateAccessTokenAsync(accessToken) != null)
            {
                return true;
            }
            else
            {
                string refreshToken = await _jsonController.GetValueByKeyAsync<string>("RefreshToken");

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    Console.WriteLine("AccessToken is invalid, refreshing for a new token");
                    var refresh = await api.Auth.RefreshAuthTokenAsync(refreshToken, _configuration["ChannelId"],  _configuration["ClientId"]);
                    api.Settings.AccessToken = refresh.AccessToken;

                    await _jsonController.UpdateAsync(jsonNode =>
                    {
                        if (jsonNode is JsonObject jsonObject)
                        {
                            jsonObject["AccessToken"] = refresh.AccessToken;
                        }
                    });
                    return true;
                }
            }

            return false;
        }

        private string GetConfiguredSymbol(bool isConfigured)
        {
            return isConfigured ? "Complete" : "Incomplete";
        }

        private async Task StartApp()
        {
            bool twitchConfigured = await ValidateTwitchConfiguration(_api);

            if (!twitchConfigured)
            {
                Console.WriteLine("\nError: Twitch Configuration is incomplete. \n");
                return;
            }
            else
            {
                bool result = await _hueController.StartPollingForLinkButtonAsync("YukiDanceParty", "MyDevice", _configuration["bridgeIp"], _configuration["AppKey"]);
                if (result)
                {
                    string accessToken = _configuration["AccessToken"];

                    TwitchEventSubListener eventSubListener = new TwitchEventSubListener(_configuration["ClientId"], _configuration["ChannelId"], $"oauth:{accessToken}", _hueController);
                    const string wsstring = "wss://eventsub.wss.twitch.tv/ws";
                    const string localwsstring = "ws://127.0.0.1:8080/ws";
                    await eventSubListener.ConnectAsync(new Uri(wsstring));

                    await eventSubListener.ListenForEventsAsync();
                }
            }
        }

        private async Task ConfigureTwitchTokens()
        {
            List<string> scopes = new() { "channel:bot", "user:read:chat", "channel:read:redemptions", "user:write:chat" };

            _api.Settings.ClientId = _configuration["ClientId"];

            WebServer server = new(_configuration["RedirectUri"]);

            Console.WriteLine($"Please authorize here:\n{getAuthorizationCodeUrl(_configuration["ClientId"], _configuration["RedirectUri"], scopes)}");

            var auth = await server.Listen();

            var resp = await _api.Auth.GetAccessTokenFromCodeAsync(auth.Code, _configuration["ClientSecret"], _configuration["RedirectUri"]);

            _api.Settings.AccessToken = resp.AccessToken;

            await _jsonController.UpdateAsync(jsonNode =>
            {
                if (jsonNode is JsonObject jsonObject)
                {
                    jsonObject["RefreshToken"] = resp.RefreshToken;
                    jsonObject["AccessToken"] = resp.AccessToken;
                }
            });

            var user = (await _api.Helix.Users.GetUsersAsync()).Users[0];

            Console.WriteLine($"Authorization success!\n\nUser: {user.DisplayName} (id: {user.Id})\nAccess token: {resp.AccessToken}\nRefresh token: {resp.RefreshToken}\nExpires in: {resp.ExpiresIn}\nScopes: {string.Join(", ", resp.Scopes)}\n");
        }

        private string getAuthorizationCodeUrl(string clientId, string redirectUri, List<string> scopes)
        {
            var scopesStr = string.Join('+', scopes);
            var encodedRedirectUri = System.Web.HttpUtility.UrlEncode(redirectUri);
            return "https://id.twitch.tv/oauth2/authorize?" +
                $"client_id={clientId}&" +
                $"force_verify=true&" +
                $"redirect_uri={encodedRedirectUri}&" +
                "response_type=code&" +
                $"scope={scopesStr}&" +
                $"state=V3ab9Va609ea11e793ae92331f023611";
        }
    }
}
