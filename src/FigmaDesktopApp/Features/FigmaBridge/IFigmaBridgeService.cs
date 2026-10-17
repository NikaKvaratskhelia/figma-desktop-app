using System.Net.WebSockets;

namespace FigmaDesktopApp.Features.FigmaBridge;

public enum BridgeConnectionState
{
    Disconnected,
    Listening,
    Connected
}

public sealed record BridgeStatusChangedEventArgs(
    BridgeConnectionState State,
    string? FileKey = null,
    string? FileName = null
);

public sealed record ExecuteCodeResult(
    bool Success,
    string? ResultJson = null,
    string? Error = null,
    string? ScreenshotBase64 = null
);

public interface IFigmaBridgeService
{
    BridgeConnectionState State { get; }
    string? ConnectedFileName { get; }
    string? ConnectedFileKey { get; }

    event EventHandler<BridgeStatusChangedEventArgs>? StatusChanged;
    event EventHandler<string>? LogReceived;

    Task StartAsync(int port = 9223, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);

    Task<ExecuteCodeResult> ExecuteCodeAsync(string figmaCode, TimeSpan? timeout = null);
    Task<string?> RequestScreenshotAsync();
}
