namespace ItemTradeApp.Features.EmailsNotifications.Emails.Contracts;

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? TextBody);