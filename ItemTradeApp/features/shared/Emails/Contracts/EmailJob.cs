namespace ItemTradeApp.Features.Shared.Emails.Contracts;

public sealed record EmailJob(
    int UserId,
    string Subject,
    string HtmlBody,
    string? TextBody = null);