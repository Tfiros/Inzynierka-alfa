using System.Security.Claims;

namespace ItemTradeApp.Features.Shared;

public static class Auth0IdHandler
{
    private const string Prefix = "auth0|";

    public static string Trim(string auth0UserId)
        => auth0UserId.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ? auth0UserId.Substring(Prefix.Length)
            : auth0UserId;

    public static string EnsureAuth0WithPrefix(string auth0UserId)
        => auth0UserId.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ? auth0UserId : Prefix + auth0UserId;

    public static string? GetUserId(ClaimsPrincipal user) 
        => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
    
}