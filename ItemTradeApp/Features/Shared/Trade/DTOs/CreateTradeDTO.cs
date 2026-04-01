namespace ItemTradeApp.Features.Shared.DTOs;

public sealed record CreateTradeDTO(
    int OfferId,
    int CustomerId,
    int UserId,
    int TokenCost,
    int? MiddlemanUserId
);