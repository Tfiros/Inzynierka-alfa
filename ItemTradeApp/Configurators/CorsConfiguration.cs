namespace ItemTradeApp.Configurators;

public static class CorsConfiguration
{
    public const string PolicyName = "AppCors";

    public static IServiceCollection AddAppCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var allowedOrigins = configuration
                                 .GetSection("Cors:AllowedOrigins")
                                 .Get<string[]>()
                             ?? ["https://localhost:5173"];

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy => policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders("X-XSRF-TOKEN"));
        });

        return services;
    }

    public static IApplicationBuilder UseAppCors(this IApplicationBuilder app)
    {
        app.UseCors(PolicyName);
        return app;
    }
}