using ItemTradeApp.Features.EmaillsNotifications.Emails.Contracts;

namespace ItemTradeApp.Features.EmaillsNotifications.Emails.Services;

public interface IEmailSender
{
    Task SendAsync(EmailMessage msg, CancellationToken ct);
}