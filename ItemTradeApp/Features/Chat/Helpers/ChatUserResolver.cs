using ItemTradeApp.Features.Trades;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Chat.Helpers;
public interface IChatUserResolver
{
    Task<(User? User, string? Error)> TryGetUserAsync(string? auth0UserId, CancellationToken ct);
}
public sealed class ChatUserResolver : IChatUserResolver
{
    private readonly IUserContext _userContext;

    public ChatUserResolver(IUserContext userContext)
    {
        _userContext = userContext;
    }

    public async Task<(User? User, string? Error)> TryGetUserAsync(string? auth0UserId, CancellationToken ct)
    {
        try
        {
            var user = await _userContext.GetRequiredUserAsync(auth0UserId, ct);
            return user is null ? (null, "User not found") : (user, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }
}