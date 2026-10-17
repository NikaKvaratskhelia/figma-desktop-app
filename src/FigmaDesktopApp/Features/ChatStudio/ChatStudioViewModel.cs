using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FigmaDesktopApp.Features.FigmaBridge;
using FigmaDesktopApp.Features.GeminiAi;

namespace FigmaDesktopApp.Features.ChatStudio;

public sealed class ChatMessageItem : ObservableObject
{
    public string Sender { get; init; } = "";
    public string Text { get; set; } = "";
    public bool IsUser { get; init; }
    public string? Code { get; set; }
    public string? ScreenshotBase64 { get; set; }
    public bool HasScreenshot => !string.IsNullOrEmpty(ScreenshotBase64);
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

public partial class ChatStudioViewModel : ObservableObject
{
    private readonly IFigmaBridgeService _bridgeService;
    private readonly IGeminiAiService _geminiService;

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
            // 1. Generate Figma Plugin API script via Gemini
            var aiResponse = await _geminiService.GenerateComponentScriptAsync(prompt);
            if (!aiResponse.Success || string.IsNullOrEmpty(aiResponse.FigmaCode))
            {
                assistantMsg.Text = $"❌ Generation error: {aiResponse.Error ?? "Could not generate code"}";
                return;
            }

            assistantMsg.Code = aiResponse.FigmaCode;
            assistantMsg.Text = $"{aiResponse.Explanation}\n\nExecuting on your Figma canvas...";

            // 2. Execute script directly in Figma through local bridge
            var execResult = await _bridgeService.ExecuteCodeAsync(aiResponse.FigmaCode);
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
        }
        catch (Exception ex)
        {
            assistantMsg.Text = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
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
