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
            Console.WriteLine("Welcome To Yuki's Disco Lights");
            Console.WriteLine("Select an option:");
            Console.WriteLine($"1. Connect to Twitch ({GetConfiguredSymbol(twitchConfigured)})");
            Console.WriteLine("2. Start Bot");
        }

        public async static Task ValidateHueConfiguration()
        {
            JsonNode? bridgeIp = await jsonController.GetValueByKeyAsync("bridgeIp");
            JsonNode? bridgeId = await jsonController.GetValueByKeyAsync("bridgeId");

            string bridgeIpValue = bridgeIp.GetValue<string>();
            string bridgeIdValue = bridgeId.GetValue<string>();

            if (string.IsNullOrEmpty(bridgeIpValue) || string.IsNullOrEmpty(bridgeIdValue))
            {
                await hueController.DiscoverBridgeAsync();
            }
        }

        private static async Task<bool> ValidateTwitchConfiguration(TwitchLib.Api.TwitchAPI api)
        {
            JsonNode? jsonData = await jsonController.GetValueByKeyAsync("AccessToken");
            string accessToken = jsonData.GetValue<string>();
            Console.WriteLine(await api.Auth.ValidateAccessTokenAsync("accessToken"));
            if (!string.IsNullOrEmpty(accessToken) && await api.Auth.ValidateAccessTokenAsync("accessToken") != null)
            {
                return true;
            }
            else
            {
                JsonNode? refreshTokenJson = await jsonController.GetValueByKeyAsync("RefreshToken");
                string refreshToken = refreshTokenJson.GetValue<string>();

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
            JsonNode? bridgeIp = await jsonController.GetValueByKeyAsync("bridgeIp");
            string bridgeIpValue = bridgeIp.GetValue<string>();
            JsonNode? appKey = await jsonController.GetValueByKeyAsync("AppKey");
            string appKeyValue = appKey.GetValue<string>();

            bool result = await hueController.StartPollingForLinkButtonAsync("YukiDanceParty", "MyDevice", bridgeIpValue, appKeyValue);
            if (result == true)
            {
                JsonNode AccessTokenJson = await jsonController.GetValueByKeyAsync("AccessToken");
                string AccessToken = AccessTokenJson.GetValue<string>();
                TwitchEventSubListener eventSubListener = new TwitchEventSubListener(config.ClientId, config.ChannelId, $"oauth:{AccessToken}", hueController);
                const string wsstring = "wss://eventsub.wss.twitch.tv/ws";
                const string localwsstring = "ws://127.0.0.1:8080/ws";
                await eventSubListener.ConnectAsync(new Uri(localwsstring));

                await eventSubListener.ListenForEventsAsync();
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
