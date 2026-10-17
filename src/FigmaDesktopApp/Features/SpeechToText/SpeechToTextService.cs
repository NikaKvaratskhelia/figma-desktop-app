using System.Speech.Recognition;

namespace FigmaDesktopApp.Features.SpeechToText;

public sealed class SpeechToTextService : ISpeechToTextService
{
    private SpeechRecognitionEngine? _engine;
    private bool _isListening;
    private bool _isInitialized;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public bool IsListening => _isListening;
    public bool IsSupported { get; private set; } = true;

    public event Action<string>? HypothesisGenerated;
    public event Action<string>? ResultGenerated;
    public event Action<bool>? StateChanged;
    public event Action<string>? ErrorOccurred;

    private bool EnsureInitialized()
    {
        if (_isInitialized && _engine != null) return true;

        try
        {
            var installed = SpeechRecognitionEngine.InstalledRecognizers();
            if (installed == null || installed.Count == 0)
            {
                ErrorOccurred?.Invoke("No local speech recognition engine found on this Windows system.");
                IsSupported = false;
                return false;
            }

            // Find English or best matching recognizer
            var recognizerInfo = installed.FirstOrDefault(r => r.Culture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                                 ?? installed[0];

            _engine = new SpeechRecognitionEngine(recognizerInfo);

            try
            {
                _engine.SetInputToDefaultAudioDevice();
            }
            catch (Exception ex)
            {
                _engine.Dispose();
                _engine = null;
                ErrorOccurred?.Invoke($"Microphone input not found: {ex.Message}. Please check Windows audio settings.");
                return false;
            }

            var dictation = new DictationGrammar();
            _engine.LoadGrammar(dictation);

            _engine.SpeechHypothesized += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Result?.Text))
                {
                    HypothesisGenerated?.Invoke(e.Result.Text);
                }
            };

            _engine.SpeechRecognized += (s, e) =>
            {
                if (e.Result != null && !string.IsNullOrWhiteSpace(e.Result.Text))
                {
                    ResultGenerated?.Invoke(e.Result.Text);
                }
            };

            _engine.RecognizeCompleted += (s, e) =>
            {
                if (_isListening)
                {
                    _isListening = false;
                    StateChanged?.Invoke(false);
                }
            };

            _isInitialized = true;
            IsSupported = true;
            return true;
        }
        catch (Exception ex)
        {
            _engine?.Dispose();
            _engine = null;
            _isInitialized = false;
            ErrorOccurred?.Invoke($"Speech recognition initialization failed: {ex.Message}");
            return false;
        }
    }

    public Task<bool> StartListeningAsync()
    {
        return Task.Run(() =>
        {
            _semaphore.Wait();
            try
            {
                if (_isListening) return true;

                if (!EnsureInitialized() || _engine == null)
                    return false;

                try
                {
                    _engine.RecognizeAsync(RecognizeMode.Multiple);
                    _isListening = true;
                    StateChanged?.Invoke(true);
                    return true;
                }
                catch (Exception ex)
                {
                    _isListening = false;
                    StateChanged?.Invoke(false);
                    ErrorOccurred?.Invoke($"Could not start microphone dictation: {ex.Message}");
                    return false;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        });
    }

    public Task StopListeningAsync()
    {
        return Task.Run(() =>
        {
            _semaphore.Wait();
            try
            {
                if (!_isListening || _engine == null) return;

                try
                {
                    _engine.RecognizeAsyncStop();
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
        });
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
            if (_isListening && _engine != null)
            {
                _engine.RecognizeAsyncCancel();
            }
            _engine?.Dispose();
        }
        catch { }
        finally
        {
            _engine = null;
            _isInitialized = false;
            _isListening = false;
            _semaphore.Dispose();
        }
    }
}
