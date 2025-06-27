using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

class Program
{
    private static List<WebSocket> _clients = new List<WebSocket>();
    private static List<ChatMessage> _messageHistory = new List<ChatMessage>();
    private static readonly int MAX_HISTORY = 10;
    private static readonly object _lock = new object();

    static async Task Main(string[] args)
    {
        var wsServer = new HttpListener();
        wsServer.Prefixes.Add("http://localhost:8080/chat/");
        wsServer.Start();
        Console.WriteLine("Сервер чата запущен на ws://localhost:8080/chat/");

        while (true)
        {
            var context = await wsServer.GetContextAsync();
            if (context.Request.IsWebSocketRequest)
            {
                ProcessWebSocketRequest(context);
            }
            else
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }
    }

    private static async void ProcessWebSocketRequest(HttpListenerContext context)
    {
        var wsContext = await context.AcceptWebSocketAsync(null);
        var ws = wsContext.WebSocket;
        lock (_lock) _clients.Add(ws);

        try
        {
            await HandleClient(ws);
        }
        finally
        {
            lock (_lock) _clients.Remove(ws);
            ws.Dispose();
        }
    }

    private static async Task HandleClient(WebSocket ws)
    {
        var buffer = new byte[4096];
        var segment = new ArraySegment<byte>(buffer);

        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(segment, CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await ProcessMessage(json, ws);
            }
        }
    }

    private static async Task ProcessMessage(string json, WebSocket sender)
    {
        try
        {
            if (json.Contains("\"User\"") && json.Contains("\"Text\""))
            {
                var message = JsonSerializer.Deserialize<ChatMessage>(json);
                message.Timestamp = DateTime.Now;

                lock (_lock)
                {
                    _messageHistory.Add(message);
                    if (_messageHistory.Count > MAX_HISTORY)
                        _messageHistory.RemoveAt(0);
                }

                await Broadcast(JsonSerializer.Serialize(message));
            }
            else if (json.Contains("\"User\""))
            {
                var joinRequest = JsonSerializer.Deserialize<JoinRequest>(json);
                await SendHistory(sender);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки сообщения: {ex.Message}");
        }
    }

    private static async Task SendHistory(WebSocket ws)
    {
        List<ChatMessage> historyCopy;
        lock (_lock)
        {
            historyCopy = new List<ChatMessage>(_messageHistory);
        }

        foreach (var msg in historyCopy)
        {
            await Send(ws, JsonSerializer.Serialize(msg));
        }
    }

    private static async Task Broadcast(string json)
    {
        var data = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(data);

        List<WebSocket> clientsCopy;
        lock (_lock)
        {
            clientsCopy = new List<WebSocket>(_clients);
        }

        foreach (var client in clientsCopy)
        {
            if (client.State == WebSocketState.Open)
            {
                await client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }

    private static async Task Send(WebSocket ws, string json)
    {
        var data = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(data);
        await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
    }
}

public class ChatMessage
{
    public string User { get; set; }
    public string Text { get; set; }
    public DateTime Timestamp { get; set; }
}

public class JoinRequest
{
    public string User { get; set; }
}