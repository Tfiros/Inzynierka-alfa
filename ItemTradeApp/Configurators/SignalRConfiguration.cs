using ItemTradeApp.Features.Chat;
using ItemTradeApp.Features.Shared.Notifications;
using ItemTradeApp.Filters;
using Microsoft.AspNetCore.SignalR;

namespace ItemTradeApp.Configurators;

public static class SignalRConfiguration
{
    public static IServiceCollection AddAppSignalR(this IServiceCollection services)
    {
        services.AddSignalR(options =>
        {
            options.AddFilter<GlobalHubExceptionFilter>();
        });

        return services;
    }

    public static WebApplication MapAppHubs(this WebApplication app)
    {
        app.MapHub<NotificationsHub>("/api/hubs/notifications")
            .RequireRateLimiting("limiterGlobal");

        app.MapHub<ChatHub>("/api/hubs/chat");

        return app;
    }
}