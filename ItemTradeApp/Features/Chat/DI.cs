using ItemTradeApp.Features.Auth;
using Microsoft.AspNetCore.SignalR;

namespace ItemTradeApp.Features.Chat;

public static class DI
{
    public static IServiceCollection RegisterChatFeatureDi(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserProvider, CurrentUserProvider>();

        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IChatService, ChatService>();

        services.AddSingleton<IUserIdProvider, Auth0UserIdProvider>();
        services.AddSingleton<PresenceTracker>();

        return services;
    }
}