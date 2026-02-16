namespace ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;

public sealed record OfferUserDTO(
    int UserId,
    string Nickname,
    string? ImageUrl,
    int SuccessTradesCount,
    float Rating,
    float SuccessRate)
    ;
