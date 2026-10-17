using Microsoft.Extensions.DependencyInjection;

namespace FigmaDesktopApp.Features.SpeechToText;

public static class SpeechToTextFeatureExtensions
{
    public static IServiceCollection AddSpeechToTextFeature(this IServiceCollection services)
    {
        services.AddSingleton<ISpeechToTextService, SpeechToTextService>();
        return services;
    }
}
