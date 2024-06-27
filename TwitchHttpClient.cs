using System.Text;

    public class TwitchHttpClient
    {
        Dictionary<string, string> TwitchTypeToUrlMap = new Dictionary<string, string>(){
            {"AddSubscription", "https://api.twitch.tv/helix/eventsub/subscriptions"},
            {"ChatMessage", "https://api.twitch.tv/helix/chat/messages"},
        };
        private readonly HttpClient _httpClient;
        private readonly string _clientId;

        public TwitchHttpClient(string clientId, string oauthToken)
        {
            _clientId = clientId;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Client-ID", _clientId);
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + oauthToken);
        }

        public async Task<HttpResponseMessage> PostAsync(string type, string message)
        {
            try
            {
                TwitchTypeToUrlMap.TryGetValue(type, out string url);
                HttpResponseMessage response = await _httpClient.PostAsync(url, new StringContent(message, Encoding.UTF8, "application/json"));
                return response;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("HTTP request exception: " + e.Message);
                throw;
            }
        }
    }