using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FigmaDesktopApp.Features.FigmaBridge;
using FigmaDesktopApp.Features.GeminiAi;
using FigmaDesktopApp.Features.SpeechToText;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace FigmaDesktopApp.Features.ChatStudio;

public partial class ChatMessageItem : ObservableObject
{
    public string Sender { get; init; } = "";

    [ObservableProperty]
    private string _text = "";

    public bool IsUser { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCode))]
    private string? _code;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScreenshot))]
    private string? _screenshotBase64;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThoughts))]
    private string? _thoughts;

    [ObservableProperty]
    private string _statusBadge = "";

    [ObservableProperty]
    private string _statusBadgeBackground = "Transparent";

    [ObservableProperty]
    private string _statusBadgeForeground = "#FFFFFF";

    [ObservableProperty]
    private bool _hasStatusBadge;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQuestions))]
    private ObservableCollection<string> _questions = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuggestedOptions))]
    private ObservableCollection<string> _suggestedOptions = new();

    public bool HasQuestions => Questions != null && Questions.Count > 0;
    public bool HasSuggestedOptions => SuggestedOptions != null && SuggestedOptions.Count > 0;

    public bool IsAssistant => !IsUser;
    public bool HasCode => !string.IsNullOrEmpty(Code);
    public bool HasScreenshot => !string.IsNullOrEmpty(ScreenshotBase64);
    public bool HasThoughts => !string.IsNullOrEmpty(Thoughts);
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string FormattedTime => Timestamp.ToString("HH:mm");
}

public partial class ChatStudioViewModel : ObservableObject
{
    private readonly IFigmaBridgeService _bridgeService;
    private readonly IGeminiAiService _geminiService;
    private readonly ISpeechToTextService _speechService;
    private readonly DispatcherQueue _dispatcherQueue;
    private string _textBeforeDictation = string.Empty;

    // --- Navigation & Sidebar States ---
    [ObservableProperty]
    private bool _isSettingsOpen;

    [RelayCommand]
    public void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

    [RelayCommand]
    public void OpenSettings() => IsSettingsOpen = true;

    [RelayCommand]
    public void CloseSettings() => IsSettingsOpen = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStudioVisible))]
    [NotifyPropertyChangedFor(nameof(IsSettingsVisible))]
    private int _selectedNavIndex = 0;

    public bool IsStudioVisible => true; // Studio is always the main workspace
    public bool IsSettingsVisible => IsSettingsOpen;

    // --- Theme Mode ---
    [ObservableProperty]
    private ElementTheme _appTheme = ElementTheme.Dark;

    [ObservableProperty]
    private int _selectedThemeIndex = 0; // 0 = Dark, 1 = Light, 2 = System

    partial void OnSelectedThemeIndexChanged(int value)
    {
        AppTheme = value switch
        {
            0 => ElementTheme.Dark,
            1 => ElementTheme.Light,
            _ => ElementTheme.Default
        };
    }

    // --- Canvas Preview & Workspace States (Inspired by Imagin / Untitled Flow reference) ---
    [ObservableProperty]
    private string? _activeCanvasScreenshot;

    [ObservableProperty]
    private bool _hasCanvasScreenshot;

    [ObservableProperty]
    private bool _showCanvasPlaceholder = true;

    partial void OnActiveCanvasScreenshotChanged(string? value)
    {
        HasCanvasScreenshot = !string.IsNullOrWhiteSpace(value);
        ShowCanvasPlaceholder = string.IsNullOrWhiteSpace(value);
    }

    [ObservableProperty]
    private string _activeCanvasTitle = "Frame 01";

    [ObservableProperty]
    private string _activeCanvasTag = "A LITTLE MORE OUTSIDE";

    [ObservableProperty]
    private string _canvasZoom = "100%";

    [ObservableProperty]
    private bool _isAssistantOpen = true;

    [RelayCommand]
    public void ToggleAssistant() => IsAssistantOpen = !IsAssistantOpen;

    // --- THINK -> MAKE Dual Mode Switcher ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputPlaceholder))]
    [NotifyPropertyChangedFor(nameof(GenerateButtonText))]
    [NotifyPropertyChangedFor(nameof(ThinkButtonOpacity))]
    [NotifyPropertyChangedFor(nameof(MakeButtonOpacity))]
    private bool _isThinkMode = false;

    public double ThinkButtonOpacity => IsThinkMode ? 1.0 : 0.45;
    public double MakeButtonOpacity => !IsThinkMode ? 1.0 : 0.45;

    [RelayCommand]
    public void SetThinkMode() => IsThinkMode = true;

    [RelayCommand]
    public void SetMakeMode() => IsThinkMode = false;

    [RelayCommand]
    public void ToggleThinkMakeMode() => IsThinkMode = !IsThinkMode;

    // --- Chat Studio Input & States ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSendPrompt))]
    private string _userInput = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyPropertyChangedFor(nameof(CanSendPrompt))]
    [NotifyPropertyChangedFor(nameof(GenerateButtonText))]
    [NotifyPropertyChangedFor(nameof(InputPlaceholder))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;
    public bool CanSendPrompt => !IsBusy && !string.IsNullOrWhiteSpace(UserInput);
    public string GenerateButtonText => IsBusy 
        ? "Generating..." 
        : (IsThinkMode ? "Plan Architecture" : "Make in Figma");
    public string InputPlaceholder => IsListening
        ? "Listening... Speak your prompt now..."
        : (IsBusy 
            ? "Synthesizing with Gemini... Please wait..." 
            : (IsThinkMode ? "Ask for UI/UX planning, component architecture, or design ideas..." : "Tell me what you're imagining..."));

    // --- Speech-to-Text Dictation States ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MicButtonBackground))]
    [NotifyPropertyChangedFor(nameof(MicButtonForeground))]
    [NotifyPropertyChangedFor(nameof(MicButtonBorder))]
    [NotifyPropertyChangedFor(nameof(MicButtonToolTip))]
    [NotifyPropertyChangedFor(nameof(InputPlaceholder))]
    private bool _isListening;

    public string MicButtonBackground => IsListening ? "#DC2626" : "Transparent";
    public string MicButtonForeground => IsListening ? "#FFFFFF" : "#A78BFA";
    public string MicButtonBorder => IsListening ? "#EF4444" : "Transparent";
    public string MicButtonToolTip => IsListening 
        ? "Microphone is on (Listening...). Click to finish dictation." 
        : "Click to speak your prompt (Speech-to-Text)";

    [RelayCommand]
    public async Task ToggleDictationAsync()
    {
        if (IsListening)
        {
            await _speechService.StopListeningAsync();
        }
        else
        {
            _textBeforeDictation = UserInput?.Trim() ?? string.Empty;
            bool started = await _speechService.StartListeningAsync();
            if (!started)
            {
                ShowNotice("Speech Recognition", "Could not start microphone. Please check Windows Settings > Privacy > Microphone.", true);
            }
        }
    }

    [ObservableProperty]
    private string _activeProgressStatus = "Ready";

    // --- Generation Status Banner States ---
    [ObservableProperty]
    private bool _hasStatusBanner;

    [ObservableProperty]
    private string _statusBannerTitle = "";

    [ObservableProperty]
    private string _statusBannerDescription = "";

    [ObservableProperty]
    private string _statusBannerIcon = "\uE73E";

    [ObservableProperty]
    private string _statusBannerBg = "#063A22";

    [ObservableProperty]
    private string _statusBannerBorder = "#10B981";

    [ObservableProperty]
    private string _statusBannerForeground = "#6EE7B7";

    [RelayCommand]
    public void DismissStatusBanner()
    {
        HasStatusBanner = false;
    }

    public void ShowNotice(string title, string description, bool isError = false)
    {
        HasStatusBanner = true;
        StatusBannerTitle = title;
        StatusBannerDescription = description;
        StatusBannerIcon = isError ? "\uE783" : "\uE720";
        StatusBannerBg = isError ? "#3A1010" : "#20163B";
        StatusBannerBorder = isError ? "#EF4444" : "#7C3AED";
        StatusBannerForeground = isError ? "#FCA5A5" : "#DDD6FE";
    }

    // --- Custom Rulesets (Global & Chat-Specific) ---
    [ObservableProperty]
    private string _globalRules = "Always use Auto-Layout with generous padding and subtle rounded corners. Place components cleanly with 100px spacing.";

    [ObservableProperty]
    private string _chatRules = "";

    [RelayCommand]
    public void ClearChatRules()
    {
        ChatRules = "";
    }

    [ObservableProperty]
    private string _statusText = "Figma Disconnected";

    [ObservableProperty]
    private string _statusColor = "#FF455B";

    [ObservableProperty]
    private string _connectedFileInfo = "No file open";

    public ObservableCollection<ChatMessageItem> Messages { get; } = new();

    // --- Settings Page States ---
    [ObservableProperty]
    private string _apiKeyInput = "";

    [ObservableProperty]
    private string _activeKeyDescription = "Default Pre-configured Key (Active)";

    [ObservableProperty]
    private string _selectedModel = "gemini-3.8-flash";

    public ObservableCollection<string> AvailableModels { get; } = new()
    {
        "gemini-3.8-flash",
        "gemini-2.5-flash",
        "gemini-2.5-pro"
    };

    [ObservableProperty]
    private string _settingsMessage = "";

    [ObservableProperty]
    private string _settingsMessageColor = "#44FF88";

    [ObservableProperty]
    private bool _hasSettingsMessage;

    public ChatStudioViewModel(IFigmaBridgeService bridgeService, IGeminiAiService geminiService, ISpeechToTextService speechService)
    {
        _bridgeService = bridgeService;
        _geminiService = geminiService;
        _speechService = speechService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _speechService.StateChanged += (listening) =>
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                IsListening = listening;
                if (!listening && !string.IsNullOrWhiteSpace(_textBeforeDictation))
                {
                    UserInput = _textBeforeDictation.Trim();
                }
            });
        };

        _speechService.HypothesisGenerated += (hypothesis) =>
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                UserInput = string.IsNullOrWhiteSpace(_textBeforeDictation)
                    ? hypothesis
                    : $"{_textBeforeDictation} {hypothesis}";
            });
        };

        _speechService.ResultGenerated += (result) =>
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                _textBeforeDictation = string.IsNullOrWhiteSpace(_textBeforeDictation)
                    ? result
                    : $"{_textBeforeDictation} {result}";
                UserInput = _textBeforeDictation;
            });
        };

        _speechService.ErrorOccurred += (err) =>
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                ShowNotice("Speech Notice", err, false);
            });
        };

        _bridgeService.StatusChanged += OnBridgeStatusChanged;
        _ = _bridgeService.StartAsync();

        RefreshActiveKeyDisplay();

        Messages.Add(new ChatMessageItem
        {
            Sender = "Gemini Studio",
            Text = "Welcome to Gemini Studio for Figma. Make sure your Figma plugin is open, then describe any component or screen to generate native Auto-Layout layers live on canvas.",
            IsUser = false,
            StatusBadge = "Ready",
            StatusBadgeBackground = "#152238",
            StatusBadgeForeground = "#7CC4F8",
            HasStatusBadge = true
        });
    }

    private void RefreshActiveKeyDisplay()
    {
        if (_geminiService.IsUsingCustomKey)
        {
            var key = _geminiService.GetConfig().ApiKey;
            var masked = key.Length > 8 ? $"{key[..4]}...{key[^4..]}" : "Custom Key";
            ActiveKeyDescription = $"Custom API Key Active ({masked})";
        }
        else
        {
            ActiveKeyDescription = "Pre-configured Key Pool (5 keys, auto-rotating on quota limits)";
        }
    }

    private void OnBridgeStatusChanged(object? sender, BridgeStatusChangedEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            switch (e.State)
            {
                case BridgeConnectionState.Connected:
                    StatusText = "Figma Connected";
                    StatusColor = "#44FF88";
                    ConnectedFileInfo = string.IsNullOrEmpty(e.FileName) ? "Active File" : e.FileName;
                    break;
                case BridgeConnectionState.Listening:
                    StatusText = "Listening for Plugin...";
                    StatusColor = "#FFB700";
                    ConnectedFileInfo = "Waiting for Figma";
                    break;
                case BridgeConnectionState.Disconnected:
                default:
                    StatusText = "Disconnected";
                    StatusColor = "#FF455B";
                    ConnectedFileInfo = "No file open";
                    break;
            }
        });
    }

    private CancellationTokenSource? _generationCts;

    [RelayCommand]
    public void CancelGeneration()
    {
        if (IsBusy && _generationCts != null && !_generationCts.IsCancellationRequested)
        {
            _generationCts.Cancel();
            ActiveProgressStatus = "Cancelling task...";
            StatusBannerTitle = "Cancelling Generation...";
            StatusBannerDescription = "Stopping task and restoring your prompt...";
            StatusBannerIcon = "\uE711";
            StatusBannerBg = "#2E1B1B";
            StatusBannerBorder = "#EF4444";
            StatusBannerForeground = "#FCA5A5";
        }
    }

    [RelayCommand]
    public async Task SendPromptAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput) || IsBusy)
            return;

        string prompt = UserInput.Trim();
        UserInput = "";
        IsBusy = true;
        HasStatusBanner = true;
        StatusBannerTitle = "Designing Component with Gemini...";
        StatusBannerDescription = "Synthesizing UI hierarchy, typography, and Auto-Layout rules...";
        StatusBannerIcon = "\uE776";
        StatusBannerBg = "#20163B";
        StatusBannerBorder = "#7C3AED";
        StatusBannerForeground = "#C4B5FD";
        ActiveProgressStatus = "Designing component architecture...";

        // Extract last 8 conversation turns for contextual multi-turn assistance
        var history = Messages
            .Where(m => !string.IsNullOrWhiteSpace(m.Text))
            .TakeLast(8)
            .Select(m => (role: m.IsUser ? "user" : "model", text: m.Text))
            .ToList();

        Messages.Add(new ChatMessageItem
        {
            Sender = "You",
            Text = prompt,
            IsUser = true
        });

        var assistantMsg = new ChatMessageItem
        {
            Sender = "Gemini Studio",
            Text = "Gemini is synthesizing component hierarchy, typography, and design tokens...",
            IsUser = false,
            StatusBadge = "Designing UI...",
            StatusBadgeBackground = "#2A2A1A",
            StatusBadgeForeground = "#FFB700",
            HasStatusBadge = true
        };
        Messages.Add(assistantMsg);

        _generationCts = new CancellationTokenSource();
        var ct = _generationCts.Token;

        try
        {
            string finalPrompt = IsThinkMode 
                ? $"[UX PLANNING & ARCHITECTURE MODE: Provide layout recommendations, 2-3 clarifying questions, and suggested starter options]: {prompt}"
                : prompt;

            var aiResponse = await _geminiService.GenerateComponentScriptAsync(finalPrompt, GlobalRules, ChatRules, history, ct);
            ct.ThrowIfCancellationRequested();

            if (!aiResponse.Success)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    StatusBannerTitle = "Generation Error";
                    StatusBannerDescription = aiResponse.Error ?? "Could not generate code";
                    StatusBannerIcon = "\uE711";
                    StatusBannerBg = "#3A1218";
                    StatusBannerBorder = "#EF4444";
                    StatusBannerForeground = "#FCA5A5";
                    ActiveProgressStatus = "Generation Failed";
                    assistantMsg.StatusBadge = "✕ Generation Error";
                    assistantMsg.StatusBadgeBackground = "#3A1A1A";
                    assistantMsg.StatusBadgeForeground = "#FF455B";
                    assistantMsg.Text = $"Generation error: {aiResponse.Error ?? "Could not generate code"}";
                });
                return;
            }

            // Mode 1: Planning, Consultation & Clarifying Questions
            if (string.Equals(aiResponse.ResponseType, "plan", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(aiResponse.FigmaCode))
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    StatusBannerTitle = "Design Plan & Guidance Ready";
                    StatusBannerDescription = "Gemini provided UX architecture recommendations and clarifying questions.";
                    StatusBannerIcon = "\uE897";
                    StatusBannerBg = "#1E1B4B";
                    StatusBannerBorder = "#6366F1";
                    StatusBannerForeground = "#A5B4FC";
                    ActiveProgressStatus = "Plan & Advice Ready";
                    assistantMsg.StatusBadge = "UX Planning & Guidance";
                    assistantMsg.StatusBadgeBackground = "#261A4D";
                    assistantMsg.StatusBadgeForeground = "#A78BFA";
                    assistantMsg.Text = aiResponse.Explanation ?? "Here are the recommended layout structures and options:";
                    assistantMsg.Thoughts = aiResponse.Thoughts;

                    if (aiResponse.Questions != null && aiResponse.Questions.Count > 0)
                    {
                        assistantMsg.Questions = new ObservableCollection<string>(aiResponse.Questions);
                    }
                    if (aiResponse.SuggestedOptions != null && aiResponse.SuggestedOptions.Count > 0)
                    {
                        assistantMsg.SuggestedOptions = new ObservableCollection<string>(aiResponse.SuggestedOptions);
                    }
                });
                return;
            }

            // Mode 2: Component Generation & Execution in Figma
            _dispatcherQueue.TryEnqueue(() =>
            {
                StatusBannerTitle = "Executing on Figma Canvas...";
                StatusBannerDescription = "Rendering Auto-Layout frames, styles, and vector layers live in Figma...";
                StatusBannerIcon = "\uE8BD";
                StatusBannerBg = "#0E2A47";
                StatusBannerBorder = "#0284C7";
                StatusBannerForeground = "#7DD3FC";
                ActiveProgressStatus = "Rendering on Figma Canvas...";
                assistantMsg.Code = aiResponse.FigmaCode;
                assistantMsg.Thoughts = aiResponse.Thoughts;
                assistantMsg.StatusBadge = "Executing in Figma...";
                assistantMsg.StatusBadgeBackground = "#152238";
                assistantMsg.StatusBadgeForeground = "#7CC4F8";
                assistantMsg.Text = $"{aiResponse.Explanation}\n\nExecuting layout instructions on canvas...";

                if (aiResponse.Questions != null && aiResponse.Questions.Count > 0)
                {
                    assistantMsg.Questions = new ObservableCollection<string>(aiResponse.Questions);
                }
                if (aiResponse.SuggestedOptions != null && aiResponse.SuggestedOptions.Count > 0)
                {
                    assistantMsg.SuggestedOptions = new ObservableCollection<string>(aiResponse.SuggestedOptions);
                }
            });

            ct.ThrowIfCancellationRequested();
            var execResult = await _bridgeService.ExecuteCodeAsync(aiResponse.FigmaCode);
            ct.ThrowIfCancellationRequested();

            _dispatcherQueue.TryEnqueue(() =>
            {
                if (!execResult.Success)
                {
                    StatusBannerTitle = "Figma Execution Error";
                    StatusBannerDescription = execResult.Error ?? "Failed to run script in Figma";
                    StatusBannerIcon = "\uE711";
                    StatusBannerBg = "#3A1218";
                    StatusBannerBorder = "#EF4444";
                    StatusBannerForeground = "#FCA5A5";
                    ActiveProgressStatus = "Execution Failed";
                    assistantMsg.StatusBadge = "✕ Execution Error";
                    assistantMsg.StatusBadgeBackground = "#3A1A1A";
                    assistantMsg.StatusBadgeForeground = "#FF455B";
                    assistantMsg.Text = $"{aiResponse.Explanation}\n\nFigma Execution Error: {execResult.Error}";
                }
                else
                {
                    StatusBannerTitle = "Generation Complete! Live in Figma";
                    StatusBannerDescription = "Component has been created, styled with Auto-Layout, and focused on your canvas.";
                    StatusBannerIcon = "\uE73E";
                    StatusBannerBg = "#063A22";
                    StatusBannerBorder = "#10B981";
                    StatusBannerForeground = "#6EE7B7";
                    ActiveProgressStatus = "Live in Figma";
                    assistantMsg.StatusBadge = "✓ Finished • Live in Figma";
                    assistantMsg.StatusBadgeBackground = "#064E3B";
                    assistantMsg.StatusBadgeForeground = "#34D399";
                    assistantMsg.Text = $"{aiResponse.Explanation}\n\nThe component has been created, styled with Auto-Layout, and focused on your active Figma canvas.";

                    if (!string.IsNullOrEmpty(execResult.ScreenshotBase64))
                    {
                        assistantMsg.ScreenshotBase64 = execResult.ScreenshotBase64;
                        ActiveCanvasScreenshot = execResult.ScreenshotBase64;
                    }
                    ActiveCanvasTitle = aiResponse.Explanation ?? "Frame 01";
                }
            });
        }
        catch (OperationCanceledException)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                StatusBannerTitle = "Task Cancelled";
                StatusBannerDescription = "Generation stopped. Your prompt was restored so you can fix any mistyped text.";
                StatusBannerIcon = "\uE711";
                StatusBannerBg = "#2E1B1B";
                StatusBannerBorder = "#EF4444";
                StatusBannerForeground = "#FCA5A5";
                ActiveProgressStatus = "Cancelled";
                assistantMsg.StatusBadge = "Cancelled";
                assistantMsg.StatusBadgeBackground = "#3A1A1A";
                assistantMsg.StatusBadgeForeground = "#FF455B";
                assistantMsg.Text = "Generation was cancelled by user.";
                // Restore mistyped input for the user
                UserInput = prompt;
            });
        }
        catch (Exception ex)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                StatusBannerTitle = "Unexpected Error";
                StatusBannerDescription = ex.Message;
                StatusBannerIcon = "\uE711";
                StatusBannerBg = "#3A1218";
                StatusBannerBorder = "#EF4444";
                StatusBannerForeground = "#FCA5A5";
                ActiveProgressStatus = "Unexpected Error";
                assistantMsg.StatusBadge = "✕ Unexpected Error";
                assistantMsg.StatusBadgeBackground = "#3A1A1A";
                assistantMsg.StatusBadgeForeground = "#FF455B";
                assistantMsg.Text = $"Unexpected error: {ex.Message}";
            });
        }
        finally
        {
            _generationCts?.Dispose();
            _generationCts = null;
            _dispatcherQueue.TryEnqueue(() =>
            {
                IsBusy = false;
            });
        }
    }

    [RelayCommand]
    public void SelectSuggestion(string suggestion)
    {
        if (IsNotBusy && !string.IsNullOrWhiteSpace(suggestion))
        {
            UserInput = suggestion;
        }
    }

    [RelayCommand]
    public void CopyCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        var package = new DataPackage();
        package.SetText(code);
        Clipboard.SetContent(package);
    }

    [RelayCommand]
    public void SetNavIndex(int index)
    {
        SelectedNavIndex = index;
    }

    // --- Settings Actions ---
    [RelayCommand]
    public void SaveCustomApiKey()
    {
        if (string.IsNullOrWhiteSpace(ApiKeyInput))
        {
            SettingsMessage = "Please paste a valid API key string.";
            SettingsMessageColor = "#FFB700";
            HasSettingsMessage = true;
            return;
        }

        string cleanKey = ApiKeyInput.Trim();
        _geminiService.UpdateConfig(new GeminiConfig(ApiKey: cleanKey, Model: SelectedModel));
        ApiKeyInput = "";
        RefreshActiveKeyDisplay();

        SettingsMessage = "Custom Gemini API Key saved and activated.";
        SettingsMessageColor = "#44FF88";
        HasSettingsMessage = true;
    }

    [RelayCommand]
    public void ResetDefaultApiKey()
    {
        _geminiService.ResetToDefault();
        ApiKeyInput = "";
        RefreshActiveKeyDisplay();

        SettingsMessage = "Reverted to default pre-configured API key.";
        SettingsMessageColor = "#7CC4F8";
        HasSettingsMessage = true;
    }

    partial void OnSelectedModelChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var current = _geminiService.GetConfig();
            _geminiService.UpdateConfig(new GeminiConfig(ApiKey: current.ApiKey, Model: value));
        }
    }
}
