using Microsoft.AspNetCore.Antiforgery;

namespace ItemTradeApp.Configurators;

public static class AntiforgeryConfiguration
{
    public static IServiceCollection AddAppAntiforgery(this IServiceCollection services)
    {
        services.AddAntiforgery(o =>
        {
            o.HeaderName = "X-XSRF-TOKEN";
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.None;
        });

        return services;
    }

    public static WebApplication UseAntiforgeryValidation(this WebApplication app)
    {
        app.Use(async (ctx, next) =>
        {
            var method = ctx.Request.Method;

            var unsafeMethod =
                HttpMethods.IsPost(method) ||
                HttpMethods.IsPut(method) ||
                HttpMethods.IsPatch(method) ||
                HttpMethods.IsDelete(method);

            if (!unsafeMethod)
            {
                await next();
                return;
            }

            var path = ctx.Request.Path.Value ?? "";

            var skip =
                path.StartsWith("/api/Auth/login", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/Auth/register", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/Auth/forgot-password", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/Auth/refresh", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/Auth/csrf", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/Auth/logout", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/Notifications", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/Contact", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/hubs", StringComparison.OrdinalIgnoreCase);

            if (!skip)
            {
                var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
                await antiforgery.ValidateRequestAsync(ctx);
            }

            await next();
        });

        return app;
    }
}