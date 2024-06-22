using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HueApi.Models;
public class TwitchEventSubListener : IDisposable
{
    private readonly string _clientId;
    private readonly string _channelId;
    private readonly string _oauthToken;
    private ClientWebSocket _webSocket;
    private readonly TwitchHttpClient _twitchHttpClient;
    private HueController _hueController;

    public TwitchEventSubListener(string clientId, string channelId, string oauthToken, HueController hueController)
    {
        _hueController = hueController;
        _clientId = clientId;
        _channelId = channelId;
        _oauthToken = oauthToken;
        _twitchHttpClient = new TwitchHttpClient(clientId, _oauthToken.Split(":")[1]);
    }

    public async Task ConnectAsync()
    {
        var localWS = new Uri("ws://127.0.0.1:8080/ws");
        var twitchWS = new Uri("wss://eventsub.wss.twitch.tv/ws");
        _webSocket = new ClientWebSocket();
        _webSocket.Options.SetRequestHeader("Client-ID", _clientId);
        _webSocket.Options.SetRequestHeader("Authorization", "Bearer " + _oauthToken);
        await _webSocket.ConnectAsync(twitchWS, CancellationToken.None);
    }

    public async Task SubscribeToChannelPointRewardsAsync(string sessionId)
    {
        Console.WriteLine(sessionId);
        var subscribeMessage = new
        {
            type = "channel.channel_points_custom_reward_redemption.add",
            version = "1",
            condition = new
            {
                broadcaster_user_id = _channelId
            },
            transport = new
            {
                method = "websocket",
                session_id = sessionId,
            }
        };

        string subscribeMessageJson = JsonConvert.SerializeObject(subscribeMessage);
        await SendMessageAsync(subscribeMessageJson);
    }

    public async Task SubscribeToChannelChatMessagesAsync(string sessionId)
    {
        var subscribeMessage = new
        {
            type = "channel.chat.message",
            version = "1",
            condition = new
            {
                broadcaster_user_id = _channelId,
                user_id = _channelId
            },
            transport = new
            {
                method = "websocket",
                session_id = sessionId,
            }
        };

        string subscribeMessageJson = JsonConvert.SerializeObject(subscribeMessage);
        await SendMessageAsync(subscribeMessageJson);
    }

    private async Task SendMessageAsync(string message)
    {
        try
        {
            HttpResponseMessage response = await _twitchHttpClient.PostAsync(message);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Twitch Event Subscription created successfully.");
            }
            else
            {
                Console.WriteLine("Failed to create Twitch Event Subscription. Status code: " + response.StatusCode);
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine("HTTP request exception: " + e.Message);
        }
    }

    public async Task ListenForEventsAsync()
    {
        const int maxBufferSize = 8192;
        var buffer = new byte[maxBufferSize];
        var messageBuffer = new MemoryStream();

        try
        {
            while (_webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                Console.WriteLine(result.CloseStatus);
                Console.WriteLine(result.CloseStatusDescription);
                    // Handle WebSocket closure
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text || result.MessageType == WebSocketMessageType.Binary)
                {
                    await messageBuffer.WriteAsync(buffer, 0, result.Count);

                    if (result.EndOfMessage)
                    {
                        messageBuffer.Seek(0, SeekOrigin.Begin);
                        var payloadJson = await ReadMessageAsync(messageBuffer);
                        await HandleEventNotificationAsync(payloadJson);
                        messageBuffer.SetLength(0);
                    }
                }
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            Console.WriteLine("WebSocket connection closed prematurely.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while listening for events: {ex.Message}");
            Console.WriteLine($"{ex}");

        }
        finally
        {
            messageBuffer.Dispose();
        }
    }

    private static async Task<string> ReadMessageAsync(Stream messageBuffer)
    {
        using var reader = new StreamReader(messageBuffer, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private async Task HandleEventNotificationAsync(string payloadJson)
    {
        var payload = JObject.Parse(payloadJson);
        string messageType = (string)payload["metadata"]["message_type"];

        var handlers = new Dictionary<string, Func<JObject, Task>>
        {
            { "session_welcome", HandleSessionWelcomeAsync },
            { "session_keepalive", HandleKeepAliveAsync },
            { "notification", HandleNotificationAsync }
        };

        if (handlers.TryGetValue(messageType, out var handler))
        {
            //string sessionId = payload["payload"]["session"]["id"].ToString();
            //Console.WriteLine($"Session Id: {sessionId}");
            await handler(payload);
        }
        else
        {
            Console.WriteLine("Unhandled message type: " + messageType);
            Console.WriteLine(payload);
        }
    }

    private async Task HandleSessionWelcomeAsync(JObject payload)
    {
        string sessionId = (string)payload["payload"]["session"]["id"];
        await SubscribeToChannelPointRewardsAsync(sessionId);
        await SubscribeToChannelChatMessagesAsync(sessionId);
    }

    private async Task HandleNotificationAsync(JObject payload)
    {
        var lights = await _hueController.GetLightsAsync();
        var light = lights.Data.Last();
        // Handle notification event here
        string type = payload["payload"]["subscription"]["type"].ToString();

        if (type == "channel.channel_points_custom_reward_redemption.add")
        {
            string rewardPrompt = payload["payload"]["event"]["reward"]["prompt"].ToString();
            string rewardTitle = payload["payload"]["event"]["reward"]["title"].ToString();
            Console.WriteLine(rewardTitle);
            Console.WriteLine(rewardPrompt);
        }
        else if (type == "channel.chat.message")
        {
            string[] CommandAndAction = payload["payload"]["event"]["message"]["text"].ToString().Split(" ");

            string command = CommandAndAction[0];

            if (command == "color")
            {
                double x = Convert.ToDouble(CommandAndAction[1]);
                double y = Convert.ToDouble(CommandAndAction[2]);
                await _hueController.SetLightColorAsync(light.Id, x,y);
            }
            else if (command == "power")
            {
                string action = CommandAndAction[1];
                Console.WriteLine(action);
                if (action == "on")
                {
                    await _hueController.TurnOnLightAsync(light.Id);   
                }
                else if (action == "off")
                {
                    await _hueController.TurnOffLightAsync(light.Id);   
                }
            }
            else {
                Console.WriteLine("Wrong format");
            }
        }
    }

    private Task HandleKeepAliveAsync(JObject payload)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
