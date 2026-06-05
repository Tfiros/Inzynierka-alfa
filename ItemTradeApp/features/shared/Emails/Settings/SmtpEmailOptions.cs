namespace ItemTradeApp.Features.Shared.Emails.Settings;

public sealed record SmtpEmailOptions
{
    public string Host { get; init; } = default!;
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string SenderName { get; set; } = default!;
    public string SenderEmail { get; set; } = default!;
    public string Username { get; init; } = default!;
    public string Password { get; init; } = default!;
}