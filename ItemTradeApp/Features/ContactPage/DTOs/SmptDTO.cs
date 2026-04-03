namespace ItemTradeApp.Features.ContactPage.DTOs;

public sealed record SmptDTO(
    string Host,
    int Port,
    string SenderName,
    string SenderEmail,
    string Username,
    string Password,
    bool EnableSsl
);