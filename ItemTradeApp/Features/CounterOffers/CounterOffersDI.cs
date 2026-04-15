namespace ItemTradeApp.Features.CounterOffers;

public static class CounterOffersDI
{
    public static IServiceCollection RegisterCounterOffersDI(
        this IServiceCollection services
    )
    {
        services.AddScoped<ICounterOffersService, CounterOffersService>();
        services.AddScoped<ICounterOffersRepository, CounterOffersRepository>();

        return services;
    }
}