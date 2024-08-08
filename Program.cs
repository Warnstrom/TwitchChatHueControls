using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace TwitchChatHueControls
{
    class Program
    {
        private static async Task Main(string[] args)
        {
            try
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
            catch (Exception ex)
            {
                // Error handling with Spectre.Console for better visual output
                Console.WriteLine("                    ____________________________");
                Console.WriteLine("                   / Oops, something went wrong. \\");
                Console.WriteLine("                   \\     Please try again :3     /");
                Console.WriteLine("                   .´‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾");
                Console.WriteLine("　　　　　   __   .´ ");
                Console.WriteLine("　　　　 ／フ   フ");
                Console.WriteLine("　　　　|  .   .|");
                Console.WriteLine("　 　　／`ミ__xノ");
                Console.WriteLine("　 　 /　　 　 |");
                Console.WriteLine("　　 /　 ヽ　　ﾉ");
                Console.WriteLine(" 　 │　　 | | |");
                Console.WriteLine("／￣|　　 | | |");
                Console.WriteLine("| (￣ヽ_ヽ)_)__)");
                Console.WriteLine("＼二つ");
                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            }
            finally
            {
                AnsiConsole.Markup("[bold yellow]Press [green]Enter[/] to exit.[/]");
                Console.ReadLine();
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
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

                var choice = await RenderStartMenu();

                switch (choice)
                {
                    case 1:
                        await ConfigureTwitchTokens();
                        break;
                    case 2:
                        await StartApp();
                        break;
                    default:
                        AnsiConsole.Markup("[red]Invalid choice. Please select again.[/]\n");
                        break;
                }
            }
        }

        private async Task<int> RenderStartMenu()
        {
            bool twitchConfigured = await ValidateTwitchConfiguration(_api);
            await ValidateHueConfiguration();

            // Create a table to structure the menu
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Teal)
                .AddColumn(new TableColumn("[bold teal]Welcome To Yuki's Disco Lights[/]"));

            string twitchStatus = twitchConfigured ? "Complete" : "Incomplete";
            table.AddRow($"[bold yellow]1.[/] Connect to Twitch ([{(twitchConfigured ? "green" : "yellow")}]{twitchStatus}[/])");
            table.AddRow("[bold yellow]2.[/] Start Bot");

            AnsiConsole.Write(table);

            var prompt = new SelectionPrompt<int>()
            .Title("Please choose an option")
                .AddChoices(1, 2)
                .HighlightStyle(new Style(foreground: Color.Teal));

            // Render the prompt and get the user's selection
            int selectedOption = AnsiConsole.Prompt(prompt);

            // Return the selected option
            return selectedOption;
        }



        private async Task ValidateHueConfiguration()
        {
            BridgeValidator validator = new();

            string localBridgeIp = _configuration["bridgeIp"];
            string localBridgeId = _configuration["bridgeId"];
            string localAppKey = _configuration["AppKey"];

            if (string.IsNullOrEmpty(localBridgeIp) || string.IsNullOrEmpty(localBridgeId))
            {
                await _hueController.DiscoverBridgeAsync();
                localBridgeIp = await _jsonController.GetValueByKeyAsync<string>("bridgeIp");
                localBridgeId = await _jsonController.GetValueByKeyAsync<string>("bridgeId");
            }

            /*if (!File.Exists("huebridge_cacert.pem"))
            {
                if (!string.IsNullOrEmpty(localBridgeIp))
                {
                    await CertificateService.ConfigureCertificate(new[] { localBridgeIp, "443", "huebridge_cacert.pem" });
                }
                else
                {
                    AnsiConsole.Markup("[red]Bridge IP is missing, cannot configure the certificate.[/]\n");
                }
            }*/
        }

        private async Task<bool> ValidateTwitchConfiguration(TwitchLib.Api.TwitchAPI api)
        {
            string accessToken = _configuration["AccessToken"];

            if (!string.IsNullOrEmpty(accessToken) && await api.Auth.ValidateAccessTokenAsync(accessToken) != null)
            {
                return true;
            }
            else
            {
                string refreshToken = _configuration["RefreshToken"];

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    AnsiConsole.Markup("[yellow]AccessToken is invalid, refreshing for a new token...[/]\n");
                    var refresh = await api.Auth.RefreshAuthTokenAsync(refreshToken, _configuration["ChannelId"], _configuration["ClientId"]);
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

        private async Task StartApp()
        {
            bool twitchConfigured = await ValidateTwitchConfiguration(_api);

            if (!twitchConfigured)
            {
                AnsiConsole.Markup("[bold red]\nError: Twitch Configuration is incomplete.\n[/]");
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

            AnsiConsole.Markup($"Please authorize here:\n[link={getAuthorizationCodeUrl(_configuration["ClientId"], _configuration["RedirectUri"], scopes)}]Authorization Link[/]\n");

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

            AnsiConsole.Write(
                new Panel($"[bold green]Authorization success![/]\n\n[bold aqua]User:[/] {user.DisplayName} (id: {user.Id})\n[bold aqua]Access token:[/] {resp.AccessToken}\n[bold aqua]Refresh token:[/] {resp.RefreshToken}\n[bold aqua]Scopes:[/] {string.Join(", ", resp.Scopes)}")
                .BorderColor(Color.Green)
            );
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
