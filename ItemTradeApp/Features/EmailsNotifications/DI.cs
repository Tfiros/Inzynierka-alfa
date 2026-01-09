using System.Threading.Channels;
using ItemTradeApp.Features.EmaillsNotifications.Emails;
using ItemTradeApp.Features.EmaillsNotifications.Emails.Contracts;
using ItemTradeApp.Features.EmaillsNotifications.Emails.Services;
using ItemTradeApp.Features.EmaillsNotifications.Emails.Settings;
using ItemTradeApp.Features.EmailsNotifications;
using ItemTradeApp.Features.EmailsNotifications.Notifications;

namespace ItemTradeApp.Features.EmaillsNotifications;

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