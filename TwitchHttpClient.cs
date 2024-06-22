using System.Text;

    public class TwitchHttpClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _twitchEventSubscriptionUrl = "https://api.twitch.tv/helix/eventsub/subscriptions";

        public TwitchHttpClient(string clientId, string oauthToken)
        {
            _clientId = clientId;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Client-ID", _clientId);
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + oauthToken);
        }

        public async Task<HttpResponseMessage> PostAsync(string message)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.PostAsync(_twitchEventSubscriptionUrl, new StringContent(message, Encoding.UTF8, "application/json"));
                return response;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("HTTP request exception: " + e.Message);
                throw;
            }
        }
    }