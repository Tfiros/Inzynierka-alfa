using ItemTradeApp.Features.ItemsFeatures.Games;
using ItemTradeApp.Features.ItemsFeatures.Genres;
using ItemTradeApp.Features.ItemsFeatures.Items;

namespace ItemTradeApp.Features.ItemsFeatures;

public static class DI
{
    public static IServiceCollection RegisterItemsFeaturesDi(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<IGenresService, GenresService>();
        serviceCollection.AddScoped<IGenresRepository, GenresRepository>();
        
        serviceCollection.AddScoped<IGamesRepository, GamesRepository>();
        serviceCollection.AddScoped<IGamesService, GamesService>();
        
        serviceCollection.AddScoped<IItemsRepository, ItemsRepository>();
        serviceCollection.AddScoped<IItemsService, ItemsService>();
        return serviceCollection;
    }
}