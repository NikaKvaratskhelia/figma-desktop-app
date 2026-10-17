using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace FigmaDesktopApp.Features.FigmaMcp;

public sealed partial class FigmaMcpService : IFigmaMcpService
{
    private readonly HttpClient _httpClient;
    private readonly string _configFilePath;
    private FigmaMcpConfig _config;
    private McpConnectionState _state = McpConnectionState.Disconnected;

    public McpConnectionState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                StateChanged?.Invoke(this, _state);
            }
        }
    }

    public string? CurrentFileKey => _config.FileKey;
    public string? CurrentFileUrl => _config.FileUrl;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_config.AccessToken) && !string.IsNullOrWhiteSpace(_config.FileKey);

    public event EventHandler<McpConnectionState>? StateChanged;
    public event EventHandler<string>? LogReceived;

    public FigmaMcpService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dir = Path.Combine(appData, "FigmaDesktopApp");
        Directory.CreateDirectory(dir);
        _configFilePath = Path.Combine(dir, "figma_mcp_config.json");

        _config = LoadConfig();
        if (IsConfigured)
        {
            State = McpConnectionState.Connected;
        }
    }

    public FigmaMcpConfig GetConfig() => _config;

    public void SaveConfig(string? accessToken, string? fileUrlOrKey)
    {
        string cleanToken = accessToken?.Trim() ?? "";
        string cleanUrlOrKey = fileUrlOrKey?.Trim() ?? "";

        string extractedKey = ExtractFileKey(cleanUrlOrKey);
        string fileUrl = cleanUrlOrKey.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? cleanUrlOrKey
            : (string.IsNullOrEmpty(extractedKey) ? "" : $"https://www.figma.com/design/{extractedKey}");

        _config = new FigmaMcpConfig(
            AccessToken: cleanToken,
            FileKey: extractedKey,
            FileUrl: fileUrl,
            Endpoint: "https://mcp.figma.com/mcp"
        );

        try
        {
            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configFilePath, json);
            LogReceived?.Invoke(this, $"[FigmaMcp] Configuration saved. FileKey: {extractedKey}");
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke(this, $"[FigmaMcp] Error saving config: {ex.Message}");
        }

        if (IsConfigured)
        {
            State = McpConnectionState.Connected;
        }
        else
        {
            State = McpConnectionState.Disconnected;
        }
    }

    public void ClearConfig()
    {
        _config = new FigmaMcpConfig("", "", "", "https://mcp.figma.com/mcp");
        try
        {
            if (File.Exists(_configFilePath))
            {
                File.Delete(_configFilePath);
            }
        }
        catch { }

        State = McpConnectionState.Disconnected;
        LogReceived?.Invoke(this, "[FigmaMcp] Configuration cleared.");
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_config.AccessToken))
        {
            LogReceived?.Invoke(this, "[FigmaMcp] Cannot test connection: No Access Token configured.");
            State = McpConnectionState.Disconnected;
            return false;
        }

        State = McpConnectionState.Connecting;
        LogReceived?.Invoke(this, "[FigmaMcp] Testing connection to https://mcp.figma.com/mcp...");

        try
        {
            var initPayload = new
            {
                jsonrpc = "2.0",
                id = "1",
                method = "initialize",
                paramsValue = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { },
                    clientInfo = new
                    {
                        name = "FigmaDesktopCompanion",
                        version = "1.0.0"
                    }
                }
            };

            // Replace paramsValue with params in JSON
            string jsonBody = "{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"FigmaDesktopCompanion\",\"version\":\"1.0.0\"}}}";

            using var request = new HttpRequestMessage(HttpMethod.Post, _config.Endpoint);
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.AccessToken);
            request.Headers.TryAddWithoutValidation("X-Figma-Token", _config.AccessToken);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            string responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                LogReceived?.Invoke(this, $"[FigmaMcp] Connection test succeeded: {responseText}");
                State = McpConnectionState.Connected;
                return true;
            }
            else
            {
                LogReceived?.Invoke(this, $"[FigmaMcp] Connection test failed ({(int)response.StatusCode} {response.ReasonPhrase}): {responseText}");
                State = McpConnectionState.Error;
                return false;
            }
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke(this, $"[FigmaMcp] Connection test exception: {ex.Message}");
            State = McpConnectionState.Error;
            return false;
        }
    }

    public async Task<McpExecutionResult> ExecuteUseFigmaAsync(
        string code,
        string? fileKey = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        string targetFileKey = !string.IsNullOrWhiteSpace(fileKey) ? fileKey.Trim() : _config.FileKey;

        if (string.IsNullOrWhiteSpace(_config.AccessToken))
        {
            return new McpExecutionResult(false, Error: "Figma Access Token is not configured. Please set it in Settings.");
        }

        if (string.IsNullOrWhiteSpace(targetFileKey))
        {
            return new McpExecutionResult(false, Error: "Target Figma File URL or Key is missing. Please set your active Figma file URL in Settings.");
        }

        LogReceived?.Invoke(this, $"[FigmaMcp] Executing use_figma tool for fileKey: {targetFileKey}...");

        try
        {
            var rpcObject = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = Guid.NewGuid().ToString("N"),
                ["method"] = "tools/call",
                ["params"] = new JsonObject
                {
                    ["name"] = "use_figma",
                    ["arguments"] = new JsonObject
                    {
                        ["fileKey"] = targetFileKey,
                        ["code"] = code,
                        ["skillNames"] = "resource:figma-use",
                        ["description"] = description ?? "Execute Gemini generated component layout"
                    }
                }
            };

            string jsonBody = rpcObject.ToJsonString();

            using var request = new HttpRequestMessage(HttpMethod.Post, _config.Endpoint);
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.AccessToken);
            request.Headers.TryAddWithoutValidation("X-Figma-Token", _config.AccessToken);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            string responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                LogReceived?.Invoke(this, $"[FigmaMcp] MCP request error ({(int)response.StatusCode}): {responseText}");
                return new McpExecutionResult(false, Error: $"Figma MCP returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseText}");
            }

            // Parse JSON-RPC response
            var jsonNode = JsonNode.Parse(responseText);
            if (jsonNode == null)
            {
                return new McpExecutionResult(false, Error: "Invalid JSON response from Figma MCP server.");
            }

            if (jsonNode["error"] != null)
            {
                string errMsg = jsonNode["error"]?["message"]?.ToString() ?? "Unknown MCP RPC error";
                LogReceived?.Invoke(this, $"[FigmaMcp] RPC Error: {errMsg}");
                return new McpExecutionResult(false, Error: errMsg);
            }

            var resultNode = jsonNode["result"];
            string? resultJson = resultNode?.ToJsonString();
            LogReceived?.Invoke(this, $"[FigmaMcp] Execution successful: {resultJson}");

            // Check if there is screenshot image data or content text
            string? screenshotBase64 = null;
            if (resultNode?["content"] is JsonArray contentArr)
            {
                foreach (var item in contentArr)
                {
                    if (item?["type"]?.ToString() == "image" && item?["data"] != null)
                    {
                        screenshotBase64 = item["data"]?.ToString();
                        break;
                    }
                }
            }

            State = McpConnectionState.Connected;
            return new McpExecutionResult(true, ResultJson: resultJson, ScreenshotBase64: screenshotBase64);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke(this, $"[FigmaMcp] Execution exception: {ex.Message}");
            return new McpExecutionResult(false, Error: $"Exception calling Figma MCP: {ex.Message}");
        }
    }

    public static string ExtractFileKey(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";

        string trimmed = input.Trim();

        // Pattern for Figma URLs: figma.com/(design|file|board|proto)/<fileKey>/...
        var match = Regex.Match(trimmed, @"figma\.com/(?:design|file|board|proto)/([a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value;
        }

        // Direct file key fallback if it is a pure alphanumeric token (typical Figma keys are 22 chars)
        if (Regex.IsMatch(trimmed, @"^[a-zA-Z0-9]{8,32}$"))
        {
            return trimmed;
        }

        return trimmed;
    }

    private FigmaMcpConfig LoadConfig()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                string json = File.ReadAllText(_configFilePath);
                var loaded = JsonSerializer.Deserialize<FigmaMcpConfig>(json);
                if (loaded != null)
                {
                    return loaded;
                }
            }
        }
        catch { }

        return new FigmaMcpConfig("", "", "", "https://mcp.figma.com/mcp");
    }
}
