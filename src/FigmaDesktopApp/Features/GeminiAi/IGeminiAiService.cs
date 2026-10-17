namespace FigmaDesktopApp.Features.GeminiAi;

public sealed record GeminiConfig(
    string ApiKey = "",
    string Model = "gemini-3.8-flash"
);

public sealed record GeneratedComponentResponse(
    bool Success,
    string? FigmaCode = null,
    string? Explanation = null,
    string? Error = null
);

public interface IGeminiAiService
{
    bool IsConfigured { get; }
    void UpdateConfig(GeminiConfig config);
    GeminiConfig GetConfig();

    Task<GeneratedComponentResponse> GenerateComponentScriptAsync(string userPrompt, CancellationToken cancellationToken = default);
}
