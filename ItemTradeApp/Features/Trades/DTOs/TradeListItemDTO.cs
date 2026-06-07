namespace ItemTradeApp.Features.Trades.DTOs;

public sealed record InTradeUserDTO(
    int UserId,
    string Nickname,
    string Email,
    string? ImageUrl,
    List<ItemInfoDTO>? OfferedItems
    );

public sealed record ItemInfoDTO(
    string ItemName,
    int Quantity
);

public sealed record TradeListItemDTO(
    int TradeId,
    int OfferId,
    int TradeStatusId,
    DateTime CreationDate,
    int CreationCost,
    InTradeUserDTO Customer,
    InTradeUserDTO PostingUser,
    int? MiddlemanUserId,
    int TokensOffered,
    int TokensWanted
);