namespace ItemTradeApp.Features.Shared.TokenEscrow;

public static class DI
{
    public static IServiceCollection RegisterTokenEscrowFeaturesDi(
        this IServiceCollection services
    )
    {
        services.AddScoped<ITokenEscrow, TokenEscrow>();
        return services;
    }
}