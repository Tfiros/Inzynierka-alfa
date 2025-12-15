using ItemTradeApp.AuthZeroCommunication;
using ItemTradeApp.Features.UsersFeature.UserInfo;
using ItemTradeApp.Features.UsersFeature.UserManagement;
using ItemTradeApp.Features.UsersFeature.UserSettings;
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
        serviceCollection.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
        serviceCollection.AddScoped<IUserSettingsService, UserSettingsService>();
        serviceCollection.AddScoped<IAuthZeroManagementClient, AuthZeroAPIManagement>();
        serviceCollection.AddScoped<IUserManagementService, UserManagementService>();
        serviceCollection.AddScoped<IUserManagementRepository, UserManagementRepository>();
        return serviceCollection;
    }
}