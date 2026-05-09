namespace ItemTradeApp.Features.Chat.Helpers;

public static class ChatIdentity
{
    public static string? NormalizeAuth0UserId(string? auth0UserId)
        => string.IsNullOrWhiteSpace(auth0UserId)
            ? null
            : auth0UserId.StartsWith("auth0|")
                ? auth0UserId.Substring("auth0|".Length)
                : auth0UserId;
}