using ItemTradeApp.Features.CounterOffers;

namespace ItemTradeApp.Features.Offers;

public static class DI
{
    public static IServiceCollection RegisterOfferFeatureDi(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<IOffersRepository,OffersRepository>();
        serviceCollection.AddScoped<IUsersRepository, UsersRepository>();
        serviceCollection.AddScoped<IOffersService, OffersService>();
        serviceCollection.AddScoped<ICounterOffersService, CounterOffersService>();

        return serviceCollection;
    }
}

