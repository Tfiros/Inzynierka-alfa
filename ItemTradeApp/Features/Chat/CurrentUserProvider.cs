using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ItemTradeApp.Features.Auth;

public interface ICurrentUserProvider
{
    string? Auth0UserId { get; }
}

public sealed class CurrentUserProvider : ICurrentUserProvider
{
    private readonly IHttpContextAccessor _http;
    public CurrentUserProvider(IHttpContextAccessor http) => _http = http;

    public string? Auth0UserId
        => _http.HttpContext?.User?.FindFirstValue("sub")
           ?? _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}