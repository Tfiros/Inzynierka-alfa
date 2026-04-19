namespace ItemTradeApp.Features.ContactPage;




public static class ContactPageDI
{
    public static IServiceCollection RegisterContactPageFeatureDI(this IServiceCollection services)
    {
        services.AddScoped<IContactPageService, ContactPageService>();
        return services;
    }
}