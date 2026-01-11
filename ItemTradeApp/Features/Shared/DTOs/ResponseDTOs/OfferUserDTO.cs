namespace ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;

public sealed record OfferUserDTO(int Id, string Nickname, string? ImageUrl, int SuccessTradesCount, float Rating, float SuccessRate);
