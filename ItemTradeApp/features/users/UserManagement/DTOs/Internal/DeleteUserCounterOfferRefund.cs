namespace ItemTradeApp.Features.Users.UserManagement.DTOs.Internal;

public sealed record DeleteUserCounterOfferRefund(
    int OwnerUserId,
    int TokensOffered);