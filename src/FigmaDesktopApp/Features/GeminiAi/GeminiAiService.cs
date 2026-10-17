using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FigmaDesktopApp.Features.GeminiAi;

public sealed class GeminiAiService : IGeminiAiService
{
    private readonly HttpClient _httpClient;
    private GeminiConfig _config;

    private const string SystemPrompt = @"
You are an expert UI/UX designer and software architect specializing in the Figma Plugin API (figma.*).
When given a user prompt, you produce executable JavaScript code designed to run directly in Figma's sandboxed environment.

Key Rules:
1. Always wrap component generation inside an async IIFE or return an async function.
2. Load all necessary fonts using `await figma.loadFontAsync({ family: 'Inter', style: 'Regular' })` before assigning text characters!
3. Prefer Auto-Layout (`frame.layoutMode = 'VERTICAL'` or `'HORIZONTAL'`).
4. Set padding using `frame.paddingTop`, `frame.paddingBottom`, `frame.paddingLeft`, `frame.paddingRight`, and `frame.itemSpacing`.
5. Group elements inside a parent Section or Frame on `figma.currentPage`.
6. Select the created component using `figma.currentPage.selection = [createdFrame]` and center view with `figma.viewport.scrollAndZoomIntoView([createdFrame])`.
7. Return clean JSON containing:
   - ""code"": The raw JavaScript code string to execute in Figma.
   - ""explanation"": A concise summary of what was designed and styling decisions.
";

    public GeminiAiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _config = new GeminiConfig();
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_config.ApiKey);

    public void UpdateConfig(GeminiConfig config) => _config = config;

    public GeminiConfig GetConfig() => _config;

    public async Task<GeneratedComponentResponse> GenerateComponentScriptAsync(string userPrompt, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new GeneratedComponentResponse(false, Error: "Google Gemini API Key is missing. Please set your key in Settings.");
        }

        string model = string.IsNullOrWhiteSpace(_config.Model) ? "gemini-2.0-flash" : _config.Model;
        string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_config.ApiKey}";

        var requestBody = new JsonObject
        {
            ["system_instruction"] = new JsonObject
            {
                ["parts"] = new JsonArray
                {
                    new JsonObject { ["text"] = SystemPrompt }
                }
            },
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = new JsonArray
                    {
                        new JsonObject { ["text"] = userPrompt }
                    }
                }
            },
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0.2,
                ["response_mime_type"] = "application/json"
            }
        };

        var content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
            string responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new GeneratedComponentResponse(false, Error: $"Gemini API error ({response.StatusCode}): {responseText}");
            }

            var jsonNode = JsonNode.Parse(responseText);
            string? rawText = jsonNode?["candidates"]?[0]?["content"]?[0]?["text"]?.ToString()
                              ?? jsonNode?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

            if (string.IsNullOrEmpty(rawText))
            {
                return new GeneratedComponentResponse(false, Error: "Received an empty response from Gemini.");
            }

            // Parse returned JSON from model
            var parsed = JsonNode.Parse(rawText);
            string? code = parsed?["code"]?.ToString();
            string? explanation = parsed?["explanation"]?.ToString();

            if (string.IsNullOrEmpty(code))
            {
                return new GeneratedComponentResponse(false, Error: "Model response did not contain Figma executable code.");
            }

            return new GeneratedComponentResponse(true, FigmaCode: code, Explanation: explanation);
        }
        catch (Exception ex)
        {
            return new GeneratedComponentResponse(false, Error: $"Failed to generate component: {ex.Message}");
        }
    }
}
