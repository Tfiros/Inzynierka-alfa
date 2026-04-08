namespace ItemTradeApp.Features.ContactPage;

using Microsoft.Extensions.DependencyInjection;


public static class ContactPageDI
{
    public static IServiceCollection RegisterContactPageFeatureDI(this IServiceCollection services)
    {
        services.AddScoped<IContactPageService, ContactPageService>();
        return services;
    }
}