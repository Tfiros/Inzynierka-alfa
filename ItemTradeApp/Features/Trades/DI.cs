using ItemTradeApp.Features.Shared;
using ItemTradeApp.Features.Trades.Repositories;
using ItemTradeApp.Features.Trades.Services;

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
        services.AddScoped<IOfferRepository, OfferRepository>();
        services.AddScoped<ICounterOfferRepository, CounterOfferRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITradeCreation, TradeCreator>();
        services.AddScoped<IPostAcceptPipeline, PostAcceptPipeline>();

        return services;
    }
}