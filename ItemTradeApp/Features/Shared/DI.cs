using System.Threading.Channels;
using ItemTradeApp.Features.Shared.Emails;
using ItemTradeApp.Features.Shared.Emails.Contracts;
using ItemTradeApp.Features.Shared.Emails.Services;
using ItemTradeApp.Features.Shared.Emails.Settings;
using ItemTradeApp.Features.Shared.Notifications;
using ItemTradeApp.Features.Shared.Notifications.Repositories;
using ItemTradeApp.Features.Shared.Notifications.Services;

namespace ItemTradeApp.Features.Shared;

public static class DI
{
    public static IServiceCollection RegisterSharedFeaturesDi(
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
        services.AddScoped<IEmailsRepository, EmailsRepository>();
        services.AddHostedService<EmailBackgroundService>();
        services.AddScoped<IUserIdentityRepository, UserIdentityRepository>();
        services.AddScoped<INotificationsPublisher, NotificationsPublisher>();
        services.AddScoped<INotificationsService, NotificationsService>();
        services.AddScoped<INotificationsRepository, NotificationsRepository>();
        services.AddScoped<IEmailGenerationService, EmailGenerationService>();
        services.AddScoped<INotificationSender, NotificationSender>();
        
        services.AddSingleton<IEmailTemplateRenderer, RazorEmailTemplateRenderer>();
        return services;
    }
}