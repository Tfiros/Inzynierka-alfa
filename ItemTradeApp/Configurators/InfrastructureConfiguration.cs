using FluentValidation;
using ItemTradeApp.Middlewares;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Configurators;

public static class InfrastructureConfiguration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto;

            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddAppRateLimiter();
        services.AddHttpClient();
        services.AddSingleton(TimeProvider.System);

        services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Scoped);

        services.Configure<ApiBehaviorOptions>(o =>
        {
            o.SuppressModelStateInvalidFilter = true;
        });

        return services;
    }
}