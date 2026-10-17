using Microsoft.Extensions.DependencyInjection;

namespace FigmaDesktopApp.Features.GeminiAi;

public static class GeminiAiExtensions
{
    public static IServiceCollection AddGeminiAiFeature(this IServiceCollection services)
    {
        services.AddHttpClient<IGeminiAiService, GeminiAiService>();
        return services;
    }
}
