using System.Security.Claims;
using ItemTradeApp;
using ItemTradeApp.Features.ItemsManagement;
using ItemTradeApp.Features.Users;
using ItemTradeApp.Middlewares;
using ItemTradeApp.Middlewares.Requirements;
using ItemTradeApp.Persistence;
using ItemTradeApp.Users.AuthZeroCommunication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(options =>
   options.UseNpgsql(builder.Configuration.GetConnectionString("DBConnection")));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ItemTradeApp", Version = "v1" });

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
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { scheme, Array.Empty<string>() } });
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

builder.Services.AddScoped<IAuthorizationHandler, OwnResourceHanlder>();
builder.Services.Configure<AuthZeroOptions>(builder.Configuration.GetSection("Auth0"));
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("AppCors", p => p
        .WithOrigins("https://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders("X-XSRF-TOKEN"));
});

builder.Services.AddHttpClient();
builder.Services.RegisterUserFeatureDi();
builder.Services.RegisterItemsFeaturesDi();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseCors("AppCors");
app.UseAuthentication();
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
        path.StartsWith("/api/Auth/logout", StringComparison.OrdinalIgnoreCase);

    if (!skip)
    {
        var antiforgery = ctx.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
        await antiforgery.ValidateRequestAsync(ctx);
    }

    await next();
});

app.MapGroup("/api").MapControllers();
app.Run();