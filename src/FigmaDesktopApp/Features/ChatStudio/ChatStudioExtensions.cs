using Microsoft.Extensions.DependencyInjection;

namespace FigmaDesktopApp.Features.ChatStudio;

public static class ChatStudioExtensions
{
    public static IServiceCollection AddChatStudioFeature(this IServiceCollection services)
    {
        services.AddTransient<ChatStudioViewModel>();
        return services;
    }
}
