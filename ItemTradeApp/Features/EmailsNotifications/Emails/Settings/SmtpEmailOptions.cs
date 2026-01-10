namespace ItemTradeApp.Features.EmaillsNotifications.Emails.Settings;

public sealed record SmtpEmailOptions
{
    public string Host { get; init; } = default!;
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string SenderName { get; set; }
    public string SenderEmail { get; set; }
    public string Username { get; init; } = default!;
    public string Password { get; init; } = default!;
}