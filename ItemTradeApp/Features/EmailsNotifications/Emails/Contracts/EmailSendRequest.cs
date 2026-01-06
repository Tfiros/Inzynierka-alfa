namespace ItemTradeApp.Features.EmaillsNotifications.Emails.Contracts;

public sealed record EmailSendRequest(
    int UserId,
    string Subject,
    string HtmlBody,
    string? TextBody);