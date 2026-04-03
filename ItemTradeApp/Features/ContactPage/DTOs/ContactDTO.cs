namespace ItemTradeApp.Features.ContactPage.DTOs;


public sealed record ContactDTO(
    string Name,
    string Email,
    string Subject,
    string Message
);