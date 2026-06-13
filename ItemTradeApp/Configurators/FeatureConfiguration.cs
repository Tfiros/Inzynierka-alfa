using ItemTradeApp.Features.Chat;
using ItemTradeApp.Features.ContactPage;
using ItemTradeApp.Features.CounterOffers;
using ItemTradeApp.Features.Favourites;
using ItemTradeApp.Features.ItemsManagement;
using ItemTradeApp.Features.Offers;
using ItemTradeApp.Features.Shared;
using ItemTradeApp.Features.Shared.Emails.Services;
using ItemTradeApp.Features.Shared.Images;
using ItemTradeApp.Features.Shared.TokenEscrow;
using ItemTradeApp.Features.Trades;
using ItemTradeApp.Features.Users;
using ItemTradeApp.Policies;

namespace ItemTradeApp.Configurators;

public static class FeatureConfiguration
{
    public static IServiceCollection AddFeatures(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.RegisterContactPageFeatureDI();
        services.RegisterPoliciesDi();
        services.RegisterUserFeatureDi(configuration);
        services.RegisterOfferFeatureDi();
        services.RegisterTradeFeaturesDi();
        services.RegisterItemsFeaturesDi();
        services.RegisterChatFeatureDi();
        services.RegisterSharedFeaturesDi(configuration);
        services.RegisterCounterOffersDI();
        services.RegisterFavouriteFeatureDi();
        services.RegisterTokenEscrowFeaturesDi();
        services.RegisterImagesFeatureDi(configuration);

        services.AddHostedService<EmailBackgroundService>();

        return services;
    }
}