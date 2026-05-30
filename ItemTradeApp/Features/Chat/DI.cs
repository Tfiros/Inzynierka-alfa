using ItemTradeApp.Features.Chat.Helpers;
using ItemTradeApp.Features.Chat.Repositories;
using ItemTradeApp.Features.Chat.Services;
using ItemTradeApp.Features.Shared.Chat;

namespace ItemTradeApp.Features.Chat;

public static class DI
{
    public static IServiceCollection RegisterChatFeatureDi(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IChatThreadsReader, ChatThreadsReader>();
        services.AddScoped<IChatReadStateService, ChatReadStateService>();
        services.AddScoped<IChatUserResolver, ChatUserResolver>();
        services.AddScoped<IChatRealtimePublisher, ChatRealtimePublisher>();
        services.AddScoped<IChatOperations, ChatOperations>();
        services.AddScoped<IChatService, ChatService>();
        services.AddSingleton<PresenceTracker>();

        return services;
    }
}