using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
public class TwitchEventSubListener
{
    private Regex ValidHexCodePattern = new Regex("([0-9a-fA-F]{6})$");
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
        _webSocket.Options.SetRequestHeader("Client-Id", _clientId);
        _webSocket.Options.SetRequestHeader("Authorization", "Bearer " + _oauthToken);
        _webSocket.Options.SetRequestHeader("Content-Type", "application/json");
        await _webSocket.ConnectAsync(twitchWS, CancellationToken.None);
    }

    public async Task SubscribeToChannelPointRewardsAsync(string sessionId)
    {

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
        await SendMessageAsync(subscribeMessageJson, "channel.channel_points_custom_reward_redemption.add");
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
        await SendMessageAsync(subscribeMessageJson, "channel.chat.message");
    }

    private async Task SendMessageAsync(string message, string type = "")
    {
        try
        {
            HttpResponseMessage response = await _twitchHttpClient.PostAsync("AddSubscription", message);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Twitch Event {type} Subscription created successfully.");
            }
            else
            {
                Console.WriteLine($"Failed to create {type} Twitch Event Subscription. Status code: " + response.StatusCode);
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
            await handler(payload);
        }
        else
        {
            Console.WriteLine("Unhandled message type: " + messageType);
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
        try
        {
            string eventType = payload["payload"]["subscription"]["type"].ToString();
            switch (eventType)
            {
                case "channel.channel_points_custom_reward_redemption.add":
                    await HandleCustomRewardRedemptionAsync(payload);
                    break;
                case "channel.chat.message":
                    break;
                default:
                    Console.WriteLine("Unhandled event type: " + eventType);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error handling notification: " + ex.Message);
        }
    }

    private async Task HandleCustomRewardRedemptionAsync(JObject payload)
    {
        string RewardTitle = payload["payload"]["event"]["reward"]["title"].ToString();
        string UserInput = payload["payload"]["event"]["user_input"].ToString();
        string RedeemUsername = payload["payload"]["event"]["user_name"].ToString();
        string UserInputCleaned = CleanUserInput(UserInput);
        switch (RewardTitle)
        {
            case "Color":
                await HandleColorCommandAsync("left", UserInputCleaned, RedeemUsername);
                break;
            case "Change left lamp color":
                await HandleColorCommandAsync("left", UserInputCleaned, RedeemUsername);
                break;
            case "Change right lamp color":
                await HandleColorCommandAsync("right", UserInputCleaned, RedeemUsername);
                break;
            case "Turn left lamp on/off":
                //await HandlePowerCommandAsync(userInput, light);
                break;
            case "Turn right lamp on/off":
                //await HandlePowerCommandAsync(userInput, light);
                break;
            default:
                Console.WriteLine("Unknown command: " + RewardTitle);
                break;
        }
    }

    private async Task HandlePowerCommandAsync(string[] commandAndAction)
    {

        string action = commandAndAction[1].ToLower();

        switch (action)
        {
            case "on":
                break;
            case "off":
                //await _hueController.TurnOffLightAsync(light.Id);
                break;
            default:
                Console.WriteLine("Unknown power action: " + action);
                break;
        }
    }

    private static string CleanUserInput(string userInput)
    {
        userInput = userInput.Trim().ToLower();
        if (userInput.Contains('#'))
        {
            userInput = userInput.Replace("#", "");
        }
        return userInput;
    }

    private async Task HandleColorCommandAsync(string lamp, string color, string RedeemUsername)
    {
        string? BaseColor = HexColorMapDictionary.Get(color);
        if (BaseColor != null)
        {  
            await _hueController.SetLightColorAsync(lamp, BaseColor);
        }
        else if (ValidHexCodePattern.IsMatch(color))
        {
            await _hueController.SetLightColorAsync(lamp, color);
        } else 
        {
            var ErrorChatMessage = new
            {
                broadcaster_id = _channelId,
                sender_id = _channelId,
                message = $"@{RedeemUsername} Unfortunately it appears that {color} is not currently supported, or an invalid hex code was provided. Please try another color or ensure the hex code is correct.",
            };
            string ErrorChatMessageJson = JsonConvert.SerializeObject(ErrorChatMessage);
            var response = await _twitchHttpClient.PostAsync("ChatMessage", ErrorChatMessageJson);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to send message. Status code: " + response.StatusCode);
            }
        }
    }

    private Task HandleKeepAliveAsync(JObject payload)
    {
        return Task.CompletedTask;
    }

}
