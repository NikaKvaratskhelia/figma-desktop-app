using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FigmaDesktopApp.Features.FigmaBridge;

public sealed class FigmaBridgeService : IFigmaBridgeService, IAsyncDisposable
{
    private HttpListener? _listener;
    private WebSocket? _activeWebSocket;
    private CancellationTokenSource? _serverCts;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRequests = new();
    private long _requestIdSequence;

    public BridgeConnectionState State { get; private set; } = BridgeConnectionState.Disconnected;
    public string? ConnectedFileName { get; private set; }
    public string? ConnectedFileKey { get; private set; }

    public event EventHandler<BridgeStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<string>? LogReceived;

    public Task StartAsync(int port = 9223, CancellationToken cancellationToken = default)
    {
        if (State != BridgeConnectionState.Disconnected)
            return Task.CompletedTask;

        _serverCts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();

        UpdateState(BridgeConnectionState.Listening);
        Log($"WebSocket bridge listening on port {port}. Waiting for Figma Plugin...");

        _ = AcceptConnectionsLoopAsync(_serverCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _serverCts?.Cancel();
        if (_listener is { IsListening: true })
        {
            _listener.Stop();
            _listener.Close();
        }

        if (_activeWebSocket is { State: WebSocketState.Open })
        {
            try
            {
                await _activeWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Host stopping", cancellationToken);
            }
            catch
            {
                // Ignore disconnect exceptions during teardown
            }
        }

        _activeWebSocket?.Dispose();
        _activeWebSocket = null;
        UpdateState(BridgeConnectionState.Disconnected);
    }

    private async Task AcceptConnectionsLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is { IsListening: true })
        {
            try
            {
                var context = await _listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    _ = HandleWebSocketAsync(context, ct);
                }
                else
                {
                    context.Response.StatusCode = 200;
                    byte[] buffer = "Figma Desktop App Bridge Service Active"u8.ToArray();
                    context.Response.ContentType = "text/plain";
                    context.Response.ContentLength64 = buffer.Length;
                    await context.Response.OutputStream.WriteAsync(buffer, ct);
                    context.Response.Close();
                }
            }
            catch when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"Bridge listener error: {ex.Message}");
            }
        }
    }

    private async Task HandleWebSocketAsync(HttpListenerContext context, CancellationToken ct)
    {
        try
        {
            var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
            _activeWebSocket = wsContext.WebSocket;
            UpdateState(BridgeConnectionState.Connected);
            Log("Figma Plugin connected successfully!");

            var buffer = new byte[1024 * 64];
            var ms = new MemoryStream();

            while (_activeWebSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await _activeWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _activeWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
                        UpdateState(BridgeConnectionState.Listening);
                        Log("Figma Plugin disconnected.");
                        return;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                string message = Encoding.UTF8.GetString(ms.ToArray());
                ProcessIncomingMessage(message);
            }
        }
        catch (Exception ex)
        {
            Log($"WebSocket error: {ex.Message}");
            UpdateState(BridgeConnectionState.Listening);
        }
    }

    private void ProcessIncomingMessage(string rawJson)
    {
        try
        {
            var doc = JsonNode.Parse(rawJson);
            if (doc is null) return;

            string? id = doc["id"]?.ToString();
            string? type = doc["type"]?.ToString();

            // Handle metadata updates from the plugin
            if (type == "DOCUMENT_CHANGE" || type == "INITIAL_STATE" || doc["fileContext"] != null)
            {
                var fileContext = doc["fileContext"];
                ConnectedFileName = fileContext?["fileName"]?.ToString() ?? ConnectedFileName;
                ConnectedFileKey = fileContext?["fileKey"]?.ToString() ?? ConnectedFileKey;
                StatusChanged?.Invoke(this, new BridgeStatusChangedEventArgs(State, ConnectedFileKey, ConnectedFileName));
            }

            // Check if this is a response to an awaiting request
            if (!string.IsNullOrEmpty(id) && _pendingRequests.TryRemove(id, out var tcs))
            {
                tcs.TrySetResult(rawJson);
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to parse plugin message: {ex.Message}");
        }
    }

    public async Task<ExecuteCodeResult> ExecuteCodeAsync(string figmaCode, TimeSpan? timeout = null)
    {
        if (_activeWebSocket == null || _activeWebSocket.State != WebSocketState.Open)
        {
            return new ExecuteCodeResult(false, Error: "Figma is not connected. Please run the Gemini Bridge plugin in Figma.");
        }

        string requestId = $"req_{Interlocked.Increment(ref _requestIdSequence)}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = tcs;

        var payload = new JsonObject
        {
            ["id"] = requestId,
            ["method"] = "EXECUTE_CODE",
            ["params"] = new JsonObject
            {
                ["code"] = figmaCode,
                ["timeout"] = 30000
            }
        };

        byte[] bytes = Encoding.UTF8.GetBytes(payload.ToJsonString());
        await _activeWebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

        TimeSpan waitTime = timeout ?? TimeSpan.FromSeconds(30);
        using var cts = new CancellationTokenSource(waitTime);

        try
        {
            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                string responseJson = await tcs.Task;
                var resNode = JsonNode.Parse(responseJson);
                var result = resNode?["result"];
                var error = resNode?["error"]?.ToString();

                if (!string.IsNullOrEmpty(error))
                {
                    return new ExecuteCodeResult(false, Error: error);
                }

                // Try to take screenshot of the newly created component
                string? screenshotBase64 = null;
                try
                {
                    screenshotBase64 = await RequestScreenshotAsync();
                }
                catch
                {
                    // Non-fatal if screenshot fails
                }

                return new ExecuteCodeResult(true, ResultJson: result?.ToJsonString(), ScreenshotBase64: screenshotBase64);
            }
        }
        catch (TaskCanceledException)
        {
            _pendingRequests.TryRemove(requestId, out _);
            return new ExecuteCodeResult(false, Error: "Execution timed out waiting for Figma response.");
        }
        catch (Exception ex)
        {
            _pendingRequests.TryRemove(requestId, out _);
            return new ExecuteCodeResult(false, Error: ex.Message);
        }
    }

    public async Task<string?> RequestScreenshotAsync()
    {
        const string screenshotScript = @"
            const selection = figma.currentPage.selection;
            const target = selection.length > 0 ? selection[0] : figma.currentPage;
            const bytes = await target.exportAsync({ format: 'PNG', constraint: { type: 'SCALE', value: 2 } });
            return figma.base64Encode(bytes);
        ";

        var res = await ExecuteCodeAsync(screenshotScript, TimeSpan.FromSeconds(10));
        if (res.Success && !string.IsNullOrEmpty(res.ResultJson))
        {
            return res.ResultJson.Trim('"');
        }
        return null;
    }

    private void UpdateState(BridgeConnectionState newState)
    {
        State = newState;
        StatusChanged?.Invoke(this, new BridgeStatusChangedEventArgs(State, ConnectedFileKey, ConnectedFileName));
    }

    private void Log(string message) => LogReceived?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] {message}");

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _serverCts?.Dispose();
    }
}
