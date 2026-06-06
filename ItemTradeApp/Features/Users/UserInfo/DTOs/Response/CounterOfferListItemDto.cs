namespace ItemTradeApp.Features.Users.UserInfo.DTOs.Response;

public sealed record CounterOfferListItemDto(
    int CounterOfferId,
    int OfferId,
    string OfferTitle,
    int OfferOwnerUserId,

    int CounterOfferUserId,
    string? OtherPartyNickname,

    DateTime CreationDate,
    int TokensOffered,
    int StatusId,
    string StatusName,

    IReadOnlyList<CounterOfferItemsDto> Items
);