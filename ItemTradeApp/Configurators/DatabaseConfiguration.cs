using ItemTradeApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Configurators;

public static class DatabaseConfiguration
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(configuration.GetConnectionString("DBConnection")));
        
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}