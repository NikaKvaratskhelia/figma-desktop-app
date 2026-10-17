using Microsoft.Extensions.DependencyInjection;

namespace FigmaDesktopApp.Features.FigmaMcp;

public static class FigmaMcpExtensions
{
    public static IServiceCollection AddFigmaMcpFeature(this IServiceCollection services)
    {
        services.AddHttpClient<IFigmaMcpService, FigmaMcpService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        return services;
    }
}
