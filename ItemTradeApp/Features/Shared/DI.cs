using System.Threading.Channels;
using ItemTradeApp.Features.EmailsNotifications;
using ItemTradeApp.Features.Shared.Emails;
using ItemTradeApp.Features.Shared.Emails.Contracts;
using ItemTradeApp.Features.Shared.Emails.Services;
using ItemTradeApp.Features.Shared.Emails.Settings;
using ItemTradeApp.Features.Shared.Notifications;

namespace ItemTradeApp.Features.Shared;

public static class DI
{
    public static IServiceCollection RegisterEmailsNotificationsFeatureDi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SmtpEmailOptions>(configuration.GetSection("Mails"));
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddSingleton(Channel.CreateBounded<EmailJob>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        }));

        services.AddSingleton<IEmailDispatcher, EmailDispatcher>();
        services.AddScoped<IEmailOutboxRepository, EmailsRepository>();
        services.AddHostedService<EmailBackgroundService>();
        services.AddScoped<IUserIdentityRepository, UserIdentityRepository>();
        services.AddScoped<INotificationsPublisher, NotificationsPublisher>();
        services.AddScoped<INotificationsService, NotificationsService>();
        services.AddScoped<INotificationsRepository, NotificationsRepository>();
        services.AddScoped<IEmailGenerationService, EmailGenerationService>();
        
        services.AddSingleton<IEmailTemplateRenderer, RazorEmailTemplateRenderer>();
        return services;
    }
}