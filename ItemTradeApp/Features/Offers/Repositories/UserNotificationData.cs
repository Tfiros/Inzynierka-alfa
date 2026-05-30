namespace ItemTradeApp.Features.Offers.DTOs.RequestDTOs;

public sealed record UserNotificationData(
    int Id,
    string Email,
    string? Nickname
);