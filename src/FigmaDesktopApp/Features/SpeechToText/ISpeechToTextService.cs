namespace FigmaDesktopApp.Features.SpeechToText;

public interface ISpeechToTextService : IDisposable
{
    bool IsListening { get; }
    bool IsSupported { get; }

    event Action<string>? HypothesisGenerated;
    event Action<string>? ResultGenerated;
    event Action<bool>? StateChanged;
    event Action<string>? ErrorOccurred;

    Task<bool> StartListeningAsync();
    Task StopListeningAsync();
    Task ToggleListeningAsync();
}
