using ItemTradeApp.Features.Chat.Helpers;
using ItemTradeApp.Features.Chat.Services;
using Microsoft.AspNetCore.SignalR;

namespace ItemTradeApp.Features.Chat;

public static class DI
{
    public static IServiceCollection RegisterChatFeatureDi(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IChatThreadsReader, ChatThreadsReader>();
        services.AddScoped<IChatDmService, ChatDmService>();
        services.AddScoped<IChatReadStateService, ChatReadStateService>();
        services.AddScoped<IChatUserResolver, ChatUserResolver>();
        services.AddScoped<IChatRealtimePublisher, ChatRealtimePublisher>();

        services.AddScoped<IChatService, ChatService>();
        services.AddSingleton<PresenceTracker>();

        return services;
    }
}