using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FigmaDesktopApp.Features.FigmaBridge;
using FigmaDesktopApp.Features.GeminiAi;
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
    private readonly DispatcherQueue _dispatcherQueue;

    // --- Navigation (0: Studio, 1: Settings) ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStudioVisible))]
    [NotifyPropertyChangedFor(nameof(IsSettingsVisible))]
    private int _selectedNavIndex = 0;

    public bool IsStudioVisible => SelectedNavIndex == 0;
    public bool IsSettingsVisible => SelectedNavIndex == 1;

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
    public string GenerateButtonText => IsBusy ? "Rendering in Figma..." : "Generate in Figma";
    public string InputPlaceholder => IsBusy ? "Generating component in Figma... Please wait..." : "Ask Gemini to generate a Figma component (e.g. 'Dark mode pricing card with 3 tiers')...";

    [ObservableProperty]
    private string _activeProgressStatus = "Ready";

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

    public ChatStudioViewModel(IFigmaBridgeService bridgeService, IGeminiAiService geminiService)
    {
        _bridgeService = bridgeService;
        _geminiService = geminiService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

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
            ActiveKeyDescription = "Default Pre-configured Key (Active)";
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

    [RelayCommand]
    public async Task SendPromptAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput) || IsBusy)
            return;

        string prompt = UserInput.Trim();
        UserInput = "";
        IsBusy = true;
        ActiveProgressStatus = "Designing component architecture...";

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

        try
        {
            var aiResponse = await _geminiService.GenerateComponentScriptAsync(prompt);
            if (!aiResponse.Success || string.IsNullOrEmpty(aiResponse.FigmaCode))
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    ActiveProgressStatus = "Generation Failed";
                    assistantMsg.StatusBadge = "Generation Error";
                    assistantMsg.StatusBadgeBackground = "#3A1A1A";
                    assistantMsg.StatusBadgeForeground = "#FF455B";
                    assistantMsg.Text = $"Generation error: {aiResponse.Error ?? "Could not generate code"}";
                });
                return;
            }

            _dispatcherQueue.TryEnqueue(() =>
            {
                ActiveProgressStatus = "Rendering on Figma Canvas...";
                assistantMsg.Code = aiResponse.FigmaCode;
                assistantMsg.Thoughts = aiResponse.Thoughts;
                assistantMsg.StatusBadge = "Executing in Figma...";
                assistantMsg.StatusBadgeBackground = "#152238";
                assistantMsg.StatusBadgeForeground = "#7CC4F8";
                assistantMsg.Text = $"{aiResponse.Explanation}\n\nExecuting layout instructions on canvas...";
            });

            var execResult = await _bridgeService.ExecuteCodeAsync(aiResponse.FigmaCode);
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (!execResult.Success)
                {
                    ActiveProgressStatus = "Execution Failed";
                    assistantMsg.StatusBadge = "Execution Error";
                    assistantMsg.StatusBadgeBackground = "#3A1A1A";
                    assistantMsg.StatusBadgeForeground = "#FF455B";
                    assistantMsg.Text = $"{aiResponse.Explanation}\n\nFigma Execution Error: {execResult.Error}";
                }
                else
                {
                    ActiveProgressStatus = "Live in Figma";
                    assistantMsg.StatusBadge = "Live in Figma";
                    assistantMsg.StatusBadgeBackground = "#12301E";
                    assistantMsg.StatusBadgeForeground = "#44FF88";
                    assistantMsg.Text = $"{aiResponse.Explanation}\n\nThe component has been created, styled with Auto-Layout, and focused on your active Figma canvas.";

                    if (!string.IsNullOrEmpty(execResult.ScreenshotBase64))
                    {
                        assistantMsg.ScreenshotBase64 = execResult.ScreenshotBase64;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                ActiveProgressStatus = "Unexpected Error";
                assistantMsg.StatusBadge = "Unexpected Error";
                assistantMsg.StatusBadgeBackground = "#3A1A1A";
                assistantMsg.StatusBadgeForeground = "#FF455B";
                assistantMsg.Text = $"Unexpected error: {ex.Message}";
            });
        }
        finally
        {
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
