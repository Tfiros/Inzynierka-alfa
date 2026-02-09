namespace ItemTradeApp.Features.Offers;

public static class DI
{
    public static IServiceCollection RegisterOfferFeatureDi(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<IOffersRepository,OffersRepository>();
        serviceCollection.AddScoped<IUsersRepository, UsersRepository>();
        serviceCollection.AddScoped<IItemsRepository, ItemsRepository>();
        serviceCollection.AddScoped<IGamesRepository, GamesRepository>();
        serviceCollection.AddScoped<IGenresRepository, GenresRepository>();
        serviceCollection.AddScoped<IRaritiesRepository, RaritiesRepository>();
        serviceCollection.AddScoped<IOffersService, OffersService>();
        return serviceCollection;
    }
}

