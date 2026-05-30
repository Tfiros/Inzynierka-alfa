using ItemTradeApp.Features.ItemsManagement.Games;
using ItemTradeApp.Features.ItemsManagement.Genres;
using ItemTradeApp.Features.ItemsManagement.ItemRarities;
using ItemTradeApp.Features.ItemsManagement.Items;

namespace ItemTradeApp.Features.ItemsManagement;

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

        serviceCollection.AddScoped<IItemRarityRepository, ItemRarityRepository>();
        serviceCollection.AddScoped<IItemRarityService, ItemRarityService>();
        return serviceCollection;
    }
}