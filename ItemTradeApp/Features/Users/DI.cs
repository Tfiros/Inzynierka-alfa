using ItemTradeApp.Features.Users.Auth;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration;
using ItemTradeApp.Features.Users.UserInfo;
using ItemTradeApp.Features.Users.UserManagement;
using ItemTradeApp.Features.Users.UserSettings;
using ItemTradeApp.Users.AuthZeroCommunication;

namespace ItemTradeApp.Features.Users;

public static class DI
{
    public static IServiceCollection RegisterUserFeatureDi(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddHttpClient("Auth0Public", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        serviceCollection.AddHttpClient("Auth0Management", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        serviceCollection.AddScoped<IAuthZeroAPIClient, AuthZeroAPIClient>();
        serviceCollection.AddScoped<IAuthZeroManagementClient, AuthZeroAPIManagement>(); 
        serviceCollection.AddSingleton<IAuth0ManagementTokenProvider, Auth0ManagementTokenProvider>();
        
        serviceCollection.AddScoped<IAuthService, AuthService>();
        serviceCollection.AddScoped<IAuthRepository, AuthRepository>();
        serviceCollection.AddScoped<IUserInfoService, UserInfoService>();
        serviceCollection.AddScoped<IUserInfoRepository,UserInfoRepository>();
        serviceCollection.AddScoped<IUserInfoOfferService,UserInfoOfferService>();
        serviceCollection.AddScoped<IUserInfoOfferRepository,UserInfoOfferRepository>();
        serviceCollection.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
        serviceCollection.AddScoped<IUserSettingsService, UserSettingsService>();
        serviceCollection.AddScoped<IUserManagementService, UserManagementService>();
        serviceCollection.AddScoped<IUserManagementRepository, UserManagementRepository>();
        return serviceCollection;
    }
}