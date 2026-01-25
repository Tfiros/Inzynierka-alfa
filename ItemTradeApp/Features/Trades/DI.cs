using ItemTradeApp.Features.Trades.Repositories;

namespace ItemTradeApp.Features.Trades;

public static class DI
{
    public static IServiceCollection RegisterTradeFeaturesDi(
        this IServiceCollection services
        )
    {
        services.AddScoped<ITradesService, TradesService>();

        services.AddScoped<ITradeRepository, TradeRepository>();
        services.AddScoped<IOfferRepository, OfferRepository>();
        services.AddScoped<ICounterOfferRepository, CounterOfferRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}