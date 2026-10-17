using Microsoft.Extensions.DependencyInjection;

namespace FigmaDesktopApp.Features.FigmaBridge;

public static class FigmaBridgeExtensions
{
    public static IServiceCollection AddFigmaBridgeFeature(this IServiceCollection services)
    {
        services.AddSingleton<IFigmaBridgeService, FigmaBridgeService>();
        return services;
    }
}
