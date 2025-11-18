using ItemTradeApp.Features.UsersFeature.UserInfo;
using ItemTradeApp.LoginFeature;

namespace ItemTradeApp.Features.UsersFeature;

public static class DI
{
    public static IServiceCollection RegisterUserFeatureDi(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<IAuthZeroAPIClient, AuthZeroAPIClient>();
        serviceCollection.AddScoped<IAuthService, AuthService>();
        serviceCollection.AddScoped<IAuthRepository, AuthRepository>();
        serviceCollection.AddScoped<IUserInfoService, UserInfoService>();
        serviceCollection.AddScoped<IUserInfoRepository,UserInfoRepository>();
        return serviceCollection;
    }
}