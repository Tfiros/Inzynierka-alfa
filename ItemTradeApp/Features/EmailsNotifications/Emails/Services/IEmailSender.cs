using ItemTradeApp.Features.EmailsNotifications.Emails.Contracts;

namespace ItemTradeApp.Features.EmailsNotifications.Emails.Services;

public interface IEmailSender
{
    Task SendAsync(EmailMessage msg, CancellationToken ct);
}