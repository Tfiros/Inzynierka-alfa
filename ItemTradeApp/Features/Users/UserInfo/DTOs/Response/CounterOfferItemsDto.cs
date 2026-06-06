namespace ItemTradeApp.features.Users.UserInfo.DTOs.Response;

public sealed record CounterOfferItemsDto(
    int ItemId,
    string Name,
    string? PhotoUrl,
    int GameId,
    string GameName,
    int Quantity
);