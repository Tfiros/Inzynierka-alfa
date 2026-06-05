namespace ItemTradeApp.Features.Users.UserManagement.DTOs.Internal;

public sealed record DeleteUserTradeRefund(
    int CustomerId,
    int SellerId,
    int TokensOffered,
    int TokensWanted
    );