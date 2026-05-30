using ItemTradeApp.Features.Shared.TradeCreation;
using ItemTradeApp.Features.Trades.Repositories;

namespace ItemTradeApp.Features.Trades;

public static class DI
{
    public static IServiceCollection RegisterTradeFeaturesDi(
        this IServiceCollection services
    )
    {
        services.AddScoped<ITradesService, TradesService>();
        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<ITradesRequestValidator, TradesRequestValidator>();
        services.AddScoped<ITradeListQueryService, TradeListQueryService>();
        services.AddScoped<ITradeRepository, TradeRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITradeCreation, TradeCreator>();

        return services;
    }
}