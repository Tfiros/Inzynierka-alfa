using ItemTradeApp.Features.Chat;
using ItemTradeApp.Features.Shared.Notifications;

namespace ItemTradeApp.Configurators;

public static class EndpointConfiguration
{
    public static WebApplication MapAppEndpoints(this WebApplication app)
    {
        app.MapGroup("/api")
            .MapControllers()
            .RequireRateLimiting("limiterGlobal");

        app.MapHub<NotificationsHub>("/api/hubs/notifications")
            .RequireRateLimiting("limiterGlobal");

        app.MapHub<ChatHub>("/api/hubs/chat");

        return app;
    }
}