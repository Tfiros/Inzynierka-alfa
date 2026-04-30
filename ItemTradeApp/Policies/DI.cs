using ItemTradeApp.Policies.OwnResourcePolicy;
using Microsoft.AspNetCore.Authorization;

namespace ItemTradeApp.Policies;

public static class DI
{
    public static IServiceCollection RegisterPoliciesDi(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<IAuthorizationHandler, OwnResourceHanlder>();
        serviceCollection.AddScoped<IOwnResourcePolicyRepository, OwnResourcePolicyRepository>();
        return serviceCollection;
    }
}