using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FigmaDesktopApp.Features.FigmaBridge;
using FigmaDesktopApp.Features.GeminiAi;
using Microsoft.UI.Dispatching;

namespace FigmaDesktopApp.Features.ChatStudio;

public partial class ChatMessageItem : ObservableObject
{
    public string Sender { get; init; } = "";

    [ObservableProperty]
    private string _text = "";

    public bool IsUser { get; init; }
    public string? Code { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScreenshot))]
    private string? _screenshotBase64;

    public bool HasScreenshot => !string.IsNullOrEmpty(ScreenshotBase64);
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

public partial class ChatStudioViewModel : ObservableObject
{
    private readonly IFigmaBridgeService _bridgeService;
    private readonly IGeminiAiService _geminiService;
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    private string _userInput = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Figma Disconnected";

    [ObservableProperty]
    private string _statusColor = "#FF455B";

    [ObservableProperty]
    private string _connectedFileInfo = "No file open";

    [ObservableProperty]
    private string _apiKeyInput = "";

    public ObservableCollection<ChatMessageItem> Messages { get; } = new();

    public ChatStudioViewModel(IFigmaBridgeService bridgeService, IGeminiAiService geminiService)
    {
        _bridgeService = bridgeService;
        _geminiService = geminiService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _bridgeService.StatusChanged += OnBridgeStatusChanged;
        _ = _bridgeService.StartAsync();

        Messages.Add(new ChatMessageItem
        {
            Sender = "Gemini",
            Text = "Welcome to Gemini Studio for Figma! Make sure the plugin is running in Figma, type your prompt below to generate components.",
            IsUser = false
        });
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

        Messages.Add(new ChatMessageItem
        {
            Sender = "You",
            Text = prompt,
            IsUser = true
        });

        var assistantMsg = new ChatMessageItem
        {
            Sender = "Gemini",
            Text = "✨ Designing component in Figma...",
            IsUser = false
        };
        Messages.Add(assistantMsg);

        try
        {
            var aiResponse = await _geminiService.GenerateComponentScriptAsync(prompt);
            if (!aiResponse.Success || string.IsNullOrEmpty(aiResponse.FigmaCode))
            {
                assistantMsg.Text = $"❌ Generation error: {aiResponse.Error ?? "Could not generate code"}";
                return;
            }

            assistantMsg.Code = aiResponse.FigmaCode;
            assistantMsg.Text = $"{aiResponse.Explanation}\n\nExecuting on your Figma canvas...";

            var execResult = await _bridgeService.ExecuteCodeAsync(aiResponse.FigmaCode);
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (!execResult.Success)
                {
                    assistantMsg.Text += $"\n\n⚠️ Figma Execution Error: {execResult.Error}";
                }
                else
                {
                    assistantMsg.Text = $"{aiResponse.Explanation}\n\n✅ Component successfully rendered on canvas!";
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
    public void SaveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(ApiKeyInput))
        {
            _geminiService.UpdateConfig(new GeminiConfig(ApiKeyInput.Trim()));
            Messages.Add(new ChatMessageItem
            {
                Sender = "System",
                Text = "✅ Gemini API Key saved successfully!",
                IsUser = false
            });
        }
    }
}
