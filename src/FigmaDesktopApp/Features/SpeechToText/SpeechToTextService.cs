using Windows.Media.SpeechRecognition;

namespace FigmaDesktopApp.Features.SpeechToText;

public sealed class SpeechToTextService : ISpeechToTextService
{
    private SpeechRecognizer? _recognizer;
    private bool _isListening;
    private bool _isInitialized;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public bool IsListening => _isListening;
    public bool IsSupported { get; private set; } = true;

    public event Action<string>? HypothesisGenerated;
    public event Action<string>? ResultGenerated;
    public event Action<bool>? StateChanged;
    public event Action<string>? ErrorOccurred;

    private async Task<bool> EnsureInitializedAsync()
    {
        if (_isInitialized && _recognizer != null) return true;

        try
        {
            _recognizer = new SpeechRecognizer();
            var compilationResult = await _recognizer.CompileConstraintsAsync();
            if (compilationResult.Status != SpeechRecognitionResultStatus.Success)
            {
                ErrorOccurred?.Invoke($"Speech recognition initialization: {compilationResult.Status}.");
                IsSupported = false;
                return false;
            }

            _recognizer.HypothesisGenerated += (s, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Hypothesis?.Text))
                {
                    HypothesisGenerated?.Invoke(args.Hypothesis.Text);
                }
            };

            _recognizer.ContinuousRecognitionSession.ResultGenerated += (s, args) =>
            {
                if (args.Result?.Status == SpeechRecognitionResultStatus.Success &&
                    !string.IsNullOrWhiteSpace(args.Result.Text))
                {
                    ResultGenerated?.Invoke(args.Result.Text);
                }
            };

            _recognizer.ContinuousRecognitionSession.Completed += (s, args) =>
            {
                _isListening = false;
                StateChanged?.Invoke(false);
                if (args.Status != SpeechRecognitionResultStatus.Success &&
                    args.Status != SpeechRecognitionResultStatus.UserCanceled)
                {
                    ErrorOccurred?.Invoke($"Speech recognition session ended: {args.Status}");
                }
            };

            _isInitialized = true;
            IsSupported = true;
            return true;
        }
        catch (Exception ex)
        {
            _recognizer?.Dispose();
            _recognizer = null;
            _isInitialized = false;

            if ((uint)ex.HResult == 0x80045509 || 
                ex.Message.Contains("speech priv policy", StringComparison.OrdinalIgnoreCase) || 
                ex.Message.Contains("privacy policy", StringComparison.OrdinalIgnoreCase))
            {
                ErrorOccurred?.Invoke("SPEECH_PRIVACY_POLICY_NOT_ACCEPTED");
            }
            else
            {
                ErrorOccurred?.Invoke($"Speech recognition not available: {ex.Message}");
            }
            return false;
        }
    }

    public async Task<bool> StartListeningAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_isListening) return true;

            var ok = await EnsureInitializedAsync();
            if (!ok || _recognizer == null) return false;

            await _recognizer.ContinuousRecognitionSession.StartAsync();
            _isListening = true;
            StateChanged?.Invoke(true);
            return true;
        }
        catch (Exception ex)
        {
            _isListening = false;
            StateChanged?.Invoke(false);
            _recognizer?.Dispose();
            _recognizer = null;
            _isInitialized = false;

            if ((uint)ex.HResult == 0x80045509 || 
                ex.Message.Contains("speech priv policy", StringComparison.OrdinalIgnoreCase) || 
                ex.Message.Contains("privacy policy", StringComparison.OrdinalIgnoreCase))
            {
                ErrorOccurred?.Invoke("SPEECH_PRIVACY_POLICY_NOT_ACCEPTED");
            }
            else
            {
                ErrorOccurred?.Invoke($"Could not access microphone: {ex.Message}");
            }
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task StopListeningAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!_isListening || _recognizer == null) return;

            try
            {
                await _recognizer.ContinuousRecognitionSession.StopAsync();
            }
            catch { }

            _isListening = false;
            StateChanged?.Invoke(false);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ToggleListeningAsync()
    {
        if (_isListening)
        {
            await StopListeningAsync();
        }
        else
        {
            await StartListeningAsync();
        }
    }

    public void Dispose()
    {
        try
        {
            if (_isListening && _recognizer != null)
            {
                _recognizer.ContinuousRecognitionSession.CancelAsync().AsTask().Wait(500);
            }
        }
        catch { }

        _recognizer?.Dispose();
        _recognizer = null;
        _isInitialized = false;
        _isListening = false;
        _semaphore.Dispose();
    }
}
