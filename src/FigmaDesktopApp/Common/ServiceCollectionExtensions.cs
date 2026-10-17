using FigmaDesktopApp.Features.ChatStudio;
using FigmaDesktopApp.Features.FigmaBridge;
using FigmaDesktopApp.Features.FigmaMcp;
using FigmaDesktopApp.Features.GeminiAi;
using FigmaDesktopApp.Features.SpeechToText;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FigmaDesktopApp.Common;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureAppFeatures(this IServiceCollection services)
    {
        // Feature registrations
        services.AddFigmaBridgeFeature();
        services.AddFigmaMcpFeature();
        services.AddGeminiAiFeature();
        services.AddSpeechToTextFeature();
        services.AddChatStudioFeature();

        // UI Views
        services.AddTransient<MainWindow>();

        return services;
    }
}
