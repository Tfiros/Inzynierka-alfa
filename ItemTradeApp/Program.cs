using ItemTradeApp.Configurators;
using ItemTradeApp.Filters;
using ItemTradeApp.Middlewares;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDatabase(builder.Configuration);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationActionFilter>();
});

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddSwaggerDocumentation();

builder.Services.AddAppSignalR();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAppAntiforgery();
builder.Services.AddAppAuthorization();
builder.Services.AddAppCors(builder.Configuration);

builder.Services.AddFeatures(builder.Configuration);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSwaggerDocumentation();

app.UseForwardedHeaders();
app.UseGlobalExceptionHandling();
app.MapHealthChecks("/health");

if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseAppCors();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.UseAntiforgeryValidation();

app.MapGroup("/api")
    .MapControllers()
    .RequireRateLimiting("limiterGlobal");

app.MapAppHubs();

app.Run();