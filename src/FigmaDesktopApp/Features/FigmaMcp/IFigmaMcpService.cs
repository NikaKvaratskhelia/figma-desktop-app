namespace FigmaDesktopApp.Features.FigmaMcp;

public enum McpConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

public sealed record McpExecutionResult(
    bool Success,
    string? ResultJson = null,
    string? Error = null,
    string? ScreenshotBase64 = null
);

public sealed record FigmaMcpConfig(
    string AccessToken,
    string FileKey,
    string FileUrl,
    string Endpoint = "https://mcp.figma.com/mcp"
);

public interface IFigmaMcpService
{
    McpConnectionState State { get; }
    string? CurrentFileKey { get; }
    string? CurrentFileUrl { get; }
    bool IsConfigured { get; }

    event EventHandler<McpConnectionState>? StateChanged;
    event EventHandler<string>? LogReceived;

    FigmaMcpConfig GetConfig();
    void SaveConfig(string? accessToken, string? fileUrlOrKey);
    void ClearConfig();

    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    Task<McpExecutionResult> ExecuteUseFigmaAsync(string code, string? fileKey = null, string? description = null, CancellationToken cancellationToken = default);
}
