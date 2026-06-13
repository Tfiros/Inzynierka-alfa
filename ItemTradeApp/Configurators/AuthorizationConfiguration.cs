using ItemTradeApp.Policies.Requirements.OwnResourcePolicy;

namespace ItemTradeApp.Configurators;

public static class AuthorizationConfiguration
{
    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("OwnResource", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new OwnResourceRequirement());
            });
        });

        return services;
    }
}