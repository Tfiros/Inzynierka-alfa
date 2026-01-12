namespace ItemTradeApp.Features.Trades.DTOs;

public sealed record InTradeUserDTO(
    int UserId,
    string Nickname,
    string Email,
    List<ItemInfoDTO>? OfferedItems
    );

public sealed record ItemInfoDTO(
    string ItemName,
    int Quantity
);

public sealed record TradeListItemDTO(
    int TradeId,
    int OfferId,
    int TokenCost,
    int TradeStatusId,
    DateTime CreationDate,
    InTradeUserDTO Customer,
    InTradeUserDTO PostingUser,
    int? MiddlemanUserId
);