using System.Net;
using System.Security.Claims;
using ItemTradeApp.Features.Offers;
using ItemTradeApp;
using ItemTradeApp.Features.ItemsManagement;
using ItemTradeApp.Features.Trades;
using ItemTradeApp.Features.Users;
using ItemTradeApp.Middlewares;
using ItemTradeApp.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;
using ItemTradeApp.Features.ContactPage;
using ItemTradeApp.Features.CounterOffers;
using FluentValidation;
using ItemTradeApp.Filters;
using Microsoft.AspNetCore.Mvc;
using ItemTradeApp.Features.Chat;
using ItemTradeApp.Features.Shared;
using ItemTradeApp.Features.Shared.Notifications;
using Microsoft.AspNetCore.HttpOverrides;
using ItemTradeApp.Policies;
using ItemTradeApp.Policies.OwnResourcePolicy.Requirements;
using Microsoft.AspNetCore.SignalR;
using ItemTradeApp.Features.Shared.TokenEscrow;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(options =>
   options.UseNpgsql(builder.Configuration.GetConnectionString("DBConnection")));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationActionFilter>();
});
builder.Services.RegisterContactPageFeatureDI();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    var knownProxy = builder.Configuration["ForwardedHeaders:KnownProxy"];

    if (!string.IsNullOrWhiteSpace(knownProxy) &&
        IPAddress.TryParse(knownProxy, out var proxyIp))
    {
        options.KnownProxies.Add(proxyIp);
    }
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("limiterGlobal", ctx =>
    {
        var path = ctx.Request.Path.Value ?? "";
        var isAuth = path.StartsWith("/api/Auth", StringComparison.OrdinalIgnoreCase);
        
        var type = isAuth ? "auth" : "api";

        var userId =
            ctx.User.FindFirstValue("sub") ??
            ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);

        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var key = !string.IsNullOrWhiteSpace(userId)
            ? $"{type}:user:{userId}"
            : $"{type}:ip:{ip}";

        //It might need to be adjusted later
        var permitLimit = isAuth ? 20 : 30;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ItemTradeApp", Version = "v1" });

    c.CustomSchemaIds(type => type.FullName!.Replace("+", "."));

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Wpisz: Bearer {token}"
    };

    c.DocumentFilter<PrefixDocumentFilter>("/api");
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { scheme, Array.Empty<string>() }
    });
});

builder.Services.AddSignalR(opts =>
{
    opts.AddFilter<GlobalHubExceptionFilter>();
});

var domain = builder.Configuration["Auth0:Domain"];
var audience = builder.Configuration["Auth0:Audience"];
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://{domain}/";
        options.Audience  = audience;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = "https://inzynierka.com/roles"
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/api/hubs/notifications") ||
                     path.StartsWithSegments("/api/hubs/chat")))
                {
                    ctx.Token = accessToken;
                    return Task.CompletedTask;
                }

                if (ctx.Request.Cookies.TryGetValue("at", out var token) && !string.IsNullOrWhiteSpace(token))
                    ctx.Token = token;

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-XSRF-TOKEN";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.None;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OwnResource", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new OwnResourceRequirement());
    });
});

builder.Services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true);
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("AppCors", p => p
        .WithOrigins("https://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders("X-XSRF-TOKEN"));
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient();
builder.Services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Scoped);
builder.Services.RegisterPoliciesDi();
builder.Services.RegisterUserFeatureDi(builder.Configuration);
builder.Services.RegisterOfferFeatureDi();
builder.Services.RegisterTradeFeaturesDi();
builder.Services.RegisterItemsFeaturesDi();
builder.Services.RegisterChatFeatureDi();
builder.Services.RegisterSharedFeaturesDi(builder.Configuration);
builder.Services.RegisterCounterOffersDI();
builder.Services.RegisterTokenEscrowFeaturesDi();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseForwardedHeaders();
app.UseGlobalExceptionHandling();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AppCors");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.Use(async (ctx, next) =>
{
    var m = ctx.Request.Method;
    var unsafeMethod =
        HttpMethods.IsPost(m) || HttpMethods.IsPut(m) ||
        HttpMethods.IsPatch(m) || HttpMethods.IsDelete(m);

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
        var antiforgery = ctx.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
        await antiforgery.ValidateRequestAsync(ctx);
    }

    await next();
});

app.MapGroup("/api")
    .MapControllers()
    .RequireRateLimiting("limiterGlobal");

app.MapHub<NotificationsHub>("/api/hubs/notifications")
    .RequireRateLimiting("limiterGlobal");
app.MapHub<ChatHub>("/api/hubs/chat");

app.Run();