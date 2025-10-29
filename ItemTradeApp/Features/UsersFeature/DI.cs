using ItemTradeApp.LoginFeature;

namespace ItemTradeApp.Features.UsersFeature;

public static class DI
{
    public static IServiceCollection RegisterUserFeatureDi(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<IAuthZeroAPIClient, AuthZeroAPIClient>();
        serviceCollection.AddScoped<IAuthService, AuthService>();
        return serviceCollection;
    }
}