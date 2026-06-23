using System.Threading.RateLimiting;
using ItemTradeApp.Features.Shared;

namespace ItemTradeApp.Middlewares;

public static class RateLimiterExtensions
{
    public static IServiceCollection AddAppRateLimiter(
        this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("limiterGlobal", ctx =>
            {
                var path = ctx.Request.Path.Value ?? "";
                var isAuth = path.StartsWith("/api/Auth",
                    StringComparison.OrdinalIgnoreCase);

                var type = isAuth ? "auth" : "api";

                var userId = Auth0IdHandler.GetUserId(ctx.User);

                var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                var key = !string.IsNullOrWhiteSpace(userId)
                    ? $"{type}:user:{userId}"
                    : $"{type}:ip:{ip}";

                var limit = isAuth ? 50 : 100;

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: key,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            options.AddPolicy("limiterChat", ctx =>
            {
                var userId = Auth0IdHandler.GetUserId(ctx.User);

                var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                var key = !string.IsNullOrWhiteSpace(userId)
                    ? $"chat:user:{userId}"
                    : $"chat:ip:{ip}";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: key,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });
        });

        return services;
    }
}