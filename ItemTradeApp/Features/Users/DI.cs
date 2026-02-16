using ItemTradeApp.Features.Users.UserInfo;
using ItemTradeApp.Features.Users.UserManagement;
using ItemTradeApp.Features.Users.UserSettings;
using ItemTradeApp.Users.Auth;
using ItemTradeApp.Users.AuthZeroCommunication;

namespace ItemTradeApp.Features.Users;

public static class DI
{
    public static IServiceCollection RegisterUserFeatureDi(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<IAuthZeroAPIClient, AuthZeroAPIClient>();
        serviceCollection.AddScoped<IAuthService, AuthService>();
        serviceCollection.AddScoped<IAuthRepository, AuthRepository>();
        serviceCollection.AddScoped<IUserInfoService, UserInfoService>();
        serviceCollection.AddScoped<IUserInfoRepository,UserInfoRepository>();
        serviceCollection.AddScoped<IUserInfoOfferService,UserInfoOfferService>();
        serviceCollection.AddScoped<IUserInfoOfferRepository,UserInfoOfferRepository>();
        serviceCollection.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
        serviceCollection.AddScoped<IUserSettingsService, UserSettingsService>();
        serviceCollection.AddScoped<IAuthZeroManagementClient, AuthZeroAPIManagement>();
        serviceCollection.AddScoped<IUserManagementService, UserManagementService>();
        serviceCollection.AddScoped<IUserManagementRepository, UserManagementRepository>();
        return serviceCollection;
    }
}