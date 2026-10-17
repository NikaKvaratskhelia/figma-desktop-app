namespace FigmaDesktopApp.Features.GeminiAi;

public sealed record GeminiConfig(
    string ApiKey = "",
    string Model = "gemini-3.8-flash"
);

public sealed record GeneratedComponentResponse(
    bool Success,
    string? FigmaCode = null,
    string? Explanation = null,
    string? Thoughts = null,
    string? Error = null
);

public interface IGeminiAiService
{
    bool IsConfigured { get; }
    bool IsUsingCustomKey { get; }
    void UpdateConfig(GeminiConfig config);
    void ResetToDefault();
    GeminiConfig GetConfig();

    Task<GeneratedComponentResponse> GenerateComponentScriptAsync(
        string userPrompt,
        string? globalRules = null,
        string? chatRules = null,
        CancellationToken cancellationToken = default);
}
