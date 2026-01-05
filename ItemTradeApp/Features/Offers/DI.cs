namespace ItemTradeApp.Features.Offers;

public static class DI
{
    public static IServiceCollection RegisterOfferFeatureDi(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<IOffersRepository,OfferRepository>();
        serviceCollection.AddScoped<IOfferUserRepository, UserRepository>();
        serviceCollection.AddScoped<IOfferService, OfferService>();
        return serviceCollection;
    }
}

