using System.Security.Claims;
using ItemTradeApp.AuthZeroCommunication;
using ItemTradeApp.Features.UsersFeature;
using ItemTradeApp.Middlewares;
using ItemTradeApp.Middlewares.Requirements;
using ItemTradeApp.Persistence;
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

builder.Services.Configure<Auth0Options>(builder.Configuration.GetSection("Auth0"));
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("AppCors", p => p
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()); 
});

builder.Services.AddHttpClient();
builder.Services.RegisterUserFeatureDi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseCors("AppCors");

app.MapControllers();
app.Run();