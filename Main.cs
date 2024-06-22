using System.Text.Json.Nodes;

namespace TestConsole
{
    public class TwitchOAuthConfig
    {
        public string ChannelId { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string RedirectUri { get; set; }
        public string AuthorizationEndpoint { get; set; } = "https://id.twitch.tv/oauth2/authorize";
        public string TokenEndpoint { get; set; } = "https://id.twitch.tv/oauth2/token";
        public string ValidateTokenEndpoint { get; set; } = "https://id.twitch.tv/oauth2/validate";

    }
    class Program
    {
        private static TwitchLib.Api.TwitchAPI Api { get; set; } = new TwitchLib.Api.TwitchAPI();
        private static HueController hueController;
        private static readonly JsonFileController jsonController = new("tokens.json");

        private static readonly TwitchOAuthConfig config = new()
        {
            ChannelId = "",
            ClientId = "",
            ClientSecret = "",
            RedirectUri = ""
        };
         static async Task Main(string[] args)
        {
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
                        await ConfigureHueApplicationKey();
                        break;
                    case "2":
                        await ConnectToTwitchEvents();
                        break;
                    case "3":
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
            bool hueConfigured = await ValidateHueConfiguration();
            Console.WriteLine("Welcome To Yuki's Disco Lights");
            Console.WriteLine("Select an option:");
            Console.WriteLine($"1. Configure Hue Application ({GetConfiguredSymbol(hueConfigured)})");
            Console.WriteLine($"2. Connect to Twitch ({GetConfiguredSymbol(twitchConfigured)})");
            Console.WriteLine("3. Start Bot");
        }

        private static async Task<bool> ValidateHueConfiguration()
        {
            var jsonData = await jsonController.GetValueByKeyAsync("bridgeIp");
            hueController.SetBridgeIp(jsonData.GetValue<string>());
            return !string.IsNullOrEmpty(jsonData.GetValue<string>());
        }

        private static async Task<bool> ValidateTwitchConfiguration(TwitchLib.Api.TwitchAPI api)
        {
            var jsonData = await jsonController.GetValueByKeyAsync("AccessToken");
            var accessToken = jsonData.GetValue<string>();

            if (!string.IsNullOrEmpty(accessToken) && await api.Auth.ValidateAccessTokenAsync(accessToken) != null)
            {
                Console.WriteLine($"AccessToken is Valid: {accessToken}");
                return true;
            }

            Console.WriteLine("AccessToken is invalid, refreshing for a new token");
            var refreshTokenJson = await jsonController.GetValueByKeyAsync("RefreshToken");
            var refreshToken = refreshTokenJson.GetValue<string>();

            if (!string.IsNullOrEmpty(refreshToken))
            {
                var refresh = await api.Auth.RefreshAuthTokenAsync(refreshToken, config.ClientSecret, config.ClientId);
                api.Settings.AccessToken = refresh.AccessToken;

                await jsonController.UpdateAsync(jsonNode =>
                {
                    if (jsonNode is JsonObject jsonObject)
                    {
                        jsonObject["AccessToken"] = refresh.AccessToken;
                    }
                });
            }

            return false;
        }

        private static string GetConfiguredSymbol(bool isConfigured)
        {
            return isConfigured ? "✔" : "✘";
        }

        private static async Task StartApp()
        {
                bool result = await hueController.StartPollingForLinkButton("MyApp", "MyDevice");
                if (result == true) 
                {
                    JsonNode AccessTokenJson = await jsonController.GetValueByKeyAsync("AccessToken");
                    string AccessToken = AccessTokenJson.GetValue<string>();
                    
                    TwitchEventSubListener eventSubListener = new(config.ClientId, config.ChannelId, $"oauth:{AccessToken}", hueController);
                    await eventSubListener.ConnectAsync();
                    await eventSubListener.ListenForEventsAsync();
                }
        }
        public static async Task ConnectToTwitchEvents()
        {
            List<String> scopes = ["channel:bot", "user:read:chat", "channel:read:redemptions"];

            Api.Settings.ClientId = config.ClientId;

            WebServer server = new(config.RedirectUri);

            Console.WriteLine($"Please authorize here:\n{getAuthorizationCodeUrl(config.ClientId, config.RedirectUri, scopes)}");

            // listen for incoming requests
            var auth = await server.Listen();

            // exchange auth code for oauth access/refresh
            var resp = await Api.Auth.GetAccessTokenFromCodeAsync(auth.Code, config.ClientSecret, config.RedirectUri);

            // update TwitchLib's api with the recently acquired access token
            Api.Settings.AccessToken = resp.AccessToken;


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
            Console.WriteLine($"Authorization success!\n\nUser: {user.DisplayName} (id: {user.Id})\nAccess token: {resp.AccessToken}\nRefresh token: {resp.RefreshToken}\nExpires in: {resp.ExpiresIn}\nScopes: {string.Join(", ", resp.Scopes)}");
        }

        private static string getAuthorizationCodeUrl(string clientId, string redirectUri, List<string> scopes)
        {
            var scopesStr = String.Join('+', scopes);
            var encodedRedirectUri = System.Web.HttpUtility.UrlEncode(redirectUri);
            return "https://id.twitch.tv/oauth2/authorize?" +
                $"client_id={clientId}&" +
                $"redirect_uri={encodedRedirectUri}&" +
                "response_type=code&" +
                $"scope={scopesStr}";
        }

        public static async Task ConfigureHueApplicationKey()
        {
            await hueController.DiscoverBridgeAsync();

        }
    }
}
