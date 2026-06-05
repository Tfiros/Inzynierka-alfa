using ItemTradeApp.Features.Shared.Emails.Contracts;

namespace ItemTradeApp.Features.Shared.Emails.Services;

public interface IEmailSender
{
    Task SendAsync(EmailMessage msg, CancellationToken ct);
}