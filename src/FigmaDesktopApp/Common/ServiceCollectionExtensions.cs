using FigmaDesktopApp.Features.ChatStudio;
using FigmaDesktopApp.Features.FigmaBridge;
using FigmaDesktopApp.Features.GeminiAi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FigmaDesktopApp.Common;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureAppFeatures(this IServiceCollection services)
    {
        // Feature registrations
        services.AddFigmaBridgeFeature();
        services.AddGeminiAiFeature();
        services.AddChatStudioFeature();

        // UI Views
        services.AddTransient<MainWindow>();

        return services;
    }
}
