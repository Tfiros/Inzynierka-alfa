using ItemTradeApp.Features.Users.Auth;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration;
using ItemTradeApp.Features.Users.UserInfo;
using ItemTradeApp.Features.Users.UserManagement;
using ItemTradeApp.Features.Users.UserSettings;
using ItemTradeApp.Policies;

namespace ItemTradeApp.Features.Users;

public static class DI
{
    public static IServiceCollection RegisterUserFeatureDi(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        serviceCollection.Configure<AuthZeroOptions>(configuration.GetSection("Auth0"));

        serviceCollection.AddHttpClient("Auth0Public")
            .AddPolicyHandler((sp, _) =>
            {
                var logger = sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Auth0Public-Retry");

                return HttpPolicies.GetRetryPolicy(logger);
            })
            .AddPolicyHandler((sp, _) =>
            {
                var logger = sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Auth0Public-CircuitBreaker");

                return HttpPolicies.GetCircuitBreakerPolicy(logger);
            })
            .AddPolicyHandler(HttpPolicies.GetTimeoutPolicy());

        serviceCollection.AddHttpClient("Auth0Management")
            .AddPolicyHandler((sp, _) =>
            {
                var logger = sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Auth0Management-Retry");

                return HttpPolicies.GetRetryPolicy(logger);
            })
            .AddPolicyHandler((sp, _) =>
            {
                var logger = sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Auth0Management-CircuitBreaker");

                return HttpPolicies.GetCircuitBreakerPolicy(logger);
            })
            .AddPolicyHandler(HttpPolicies.GetTimeoutPolicy());

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