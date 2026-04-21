namespace ItemTradeApp.Features.CounterOffers;

public static class CounterOffersDI
{
    public static IServiceCollection RegisterCounterOffersDI(
        this IServiceCollection services
    )
    {
        services.AddScoped<ICounterOffersService, CounterOffersService>();
        services.AddScoped<ICounterOffersRepository, CounterOffersRepository>();
        services.AddScoped<IItemsRepository, ItemsRepository>();
        services.AddScoped<IOfferRepository, OfferRepository>();
        services.AddScoped<ITradeRepository, TradeRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}