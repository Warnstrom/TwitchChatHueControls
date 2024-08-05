using System.Text.Json.Nodes;
using DotNetEnv;

namespace TwitchChatHueControls
{
    public class TwitchOAuthConfig
    {
        public string ChannelId { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string RedirectUri { get; set; }

    }
    class Program
    {
        private static TwitchLib.Api.TwitchAPI Api { get; set; } = new TwitchLib.Api.TwitchAPI();
        private static HueController? hueController;
        private static readonly JsonFileController jsonController = new("tokens.json");
        private static TwitchOAuthConfig? config;
        static async Task Main(string[] args)
        {

            Env.Load();
            config = new()
            {
                ChannelId = Env.GetString("CHANNEL_ID"),
                ClientId = Env.GetString("CLIENT_ID"),
                ClientSecret = Env.GetString("CLIENT_SECRET"),
                RedirectUri = Env.GetString("REDIRECT_URI")
            };
            hueController = new HueController(jsonController);
            await StartMenu();
        }

        public static async Task StartMenu()
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

        private static async Task RenderStartMenu()
        {
            bool twitchConfigured = await ValidateTwitchConfiguration(Api);
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

        public async static Task ValidateHueConfiguration()
        {
            BridgeValidator validator = new();

            string localBridgeIp = await jsonController.GetValueByKeyAsync<string>("bridgeIp");
            string localBridgeId = await jsonController.GetValueByKeyAsync<string>("bridgeId");
            string localAppKey = await jsonController.GetValueByKeyAsync<string>("AppKey");

            // Check if Bridge IP or ID is missing
            if (string.IsNullOrEmpty(localBridgeIp) || string.IsNullOrEmpty(localBridgeId))
            {
                await hueController.DiscoverBridgeAsync();
                // Re-fetch the bridge IP and ID after discovery
                localBridgeIp = await jsonController.GetValueByKeyAsync<string>("bridgeIp");
                localBridgeId = await jsonController.GetValueByKeyAsync<string>("bridgeId");
            }

            // Only configure the certificate if the file does not exist
            if (!File.Exists("huebridge_cacert.pem"))
            {
                // Ensure that localBridgeIp is not null or empty before configuring the certificate
                if (!string.IsNullOrEmpty(localBridgeIp))
                {
                    await CertificateService.ConfigureCertificate([localBridgeIp, "443", "huebridge_cacert.pem"]);
                }
                else
                {
                    Console.WriteLine("Bridge IP is missing, cannot configure the certificate.");
                }
            }


            /*

            This feature is temporarly disabled

            if (!string.IsNullOrEmpty(localBridgeIp) && !string.IsNullOrEmpty(localBridgeId) && !string.IsNullOrEmpty(localAppKey))
            {
                bool validBridgeIp = await validator.ValidateBridgeIpAsync(localBridgeId, localBridgeIp, localAppKey);
                if (!validBridgeIp)
                {
                    Console.WriteLine("ASDASd");
                    await hueController.DiscoverBridgeAsync();
                }
            }*/
        }

        private static async Task<bool> ValidateTwitchConfiguration(TwitchLib.Api.TwitchAPI api)
        {
            string accessToken = await jsonController.GetValueByKeyAsync<string>("AccessToken");

            if (!string.IsNullOrEmpty(accessToken) && await api.Auth.ValidateAccessTokenAsync(accessToken) != null)
            {
                return true;
            }
            else
            {
                string refreshToken = await jsonController.GetValueByKeyAsync<string>("RefreshToken");

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    Console.WriteLine("AccessToken is invalid, refreshing for a new token");
                    var refresh = await api.Auth.RefreshAuthTokenAsync(refreshToken, config.ClientSecret, config.ClientId);
                    api.Settings.AccessToken = refresh.AccessToken;

                    await jsonController.UpdateAsync(jsonNode =>
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

        private static string GetConfiguredSymbol(bool isConfigured)
        {
            return isConfigured ? "Complete" : "Incomplete";
        }

        private static async Task StartApp()
        {
            bool twitchConfigured = await ValidateTwitchConfiguration(Api);

            if (twitchConfigured == false)
            {
                Console.WriteLine("\nError: Twitch Configuration is incomplete. \n");
                return;
            }
            else
            {
                string bridgeIp = await jsonController.GetValueByKeyAsync<string>("bridgeIp");
                string appKey = await jsonController.GetValueByKeyAsync<string>("AppKey");

                bool result = await hueController.StartPollingForLinkButtonAsync("YukiDanceParty", "MyDevice", bridgeIp, appKey);
                if (result == true)
                {
                    string AccessToken = await jsonController.GetValueByKeyAsync<string>("AccessToken");

                    TwitchEventSubListener eventSubListener = new TwitchEventSubListener(config.ClientId, config.ChannelId, $"oauth:{AccessToken}", hueController);
                    const string wsstring = "wss://eventsub.wss.twitch.tv/ws";
                    const string localwsstring = "ws://127.0.0.1:8080/ws";
                    await eventSubListener.ConnectAsync(new Uri(wsstring));

                    await eventSubListener.ListenForEventsAsync();
                }
            }
        }
        public static async Task ConfigureTwitchTokens()
        {
            List<String> scopes = ["channel:bot", "user:read:chat", "channel:read:redemptions", "user:write:chat"];

            Api.Settings.ClientId = config.ClientId;

            WebServer server = new(config.RedirectUri);

            Console.WriteLine($"Please authorize here:\n{getAuthorizationCodeUrl(config.ClientId, config.RedirectUri, scopes)}");

            // listen for incoming requests
            var auth = await server.Listen();

            // exchange auth code for oauth access/refresh
            var resp = await Api.Auth.GetAccessTokenFromCodeAsync(auth.Code, config.ClientSecret, config.RedirectUri);

            // update TwitchLib's api with the recently acquired access token
            Api.Settings.AccessToken = resp.AccessToken;

            // update JSON FIle with the Access and RefreshTokens
            await jsonController.UpdateAsync(jsonNode =>
            {

                if (jsonNode is JsonObject jsonObject)
                {
                    jsonObject["RefreshToken"] = resp.RefreshToken;
                    jsonObject["AccessToken"] = resp.AccessToken;
                }
            });

            // get the auth'd user 
            var user = (await Api.Helix.Users.GetUsersAsync()).Users[0];

            // print out all the data we've got
            Console.WriteLine($"Authorization success!\n\nUser: {user.DisplayName} (id: {user.Id})\nAccess token: {resp.AccessToken}\nRefresh token: {resp.RefreshToken}\nExpires in: {resp.ExpiresIn}\nScopes: {string.Join(", ", resp.Scopes)}\n");
        }

        private static string getAuthorizationCodeUrl(string clientId, string redirectUri, List<string> scopes)
        {
            var scopesStr = String.Join('+', scopes);
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
