using System.Threading.Channels;
using ItemTradeApp.Features.EmailsNotifications.Emails;
using ItemTradeApp.Features.EmailsNotifications.Emails.Contracts;
using ItemTradeApp.Features.EmailsNotifications.Emails.Services;
using ItemTradeApp.Features.EmailsNotifications.Emails.Settings;
using ItemTradeApp.Features.EmailsNotifications.Notifications;

namespace ItemTradeApp.Features.EmailsNotifications;

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
        services.AddScoped<IEmailOutboxRepository, EmailOutboxRepository>();
        services.AddHostedService<EmailBackgroundService>();
        services.AddScoped<IUserIdentityRepository, UserIdentityRepository>();
        services.AddScoped<INotificationsPublisher, NotificationsPublisher>();
        services.AddScoped<INotificationsService, NotificationsService>();
        services.AddScoped<INotificationsRepository, NotificationsRepository>();
        return services;
    }
}