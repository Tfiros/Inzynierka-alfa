using ItemTradeApp.Features.Favourites.Repositories;

namespace ItemTradeApp.Features.Favourites;

public static class DI
{
    public static IServiceCollection RegisterFavouriteFeatureDi(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<IFavouritesRepository, FavouritesRepository>();
        serviceCollection.AddScoped<IUserRepository, UserRepository>();
        serviceCollection.AddScoped<IOffersRepository, OffersRepository>();
        
        serviceCollection.AddScoped<IFavouritesService, FavouritesService>();

        return serviceCollection;
    }
}