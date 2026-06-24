using System.Security.Claims;
using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Shared;
using ItemTradeApp.Features.Users.Auth.DTOs.RequestDtos;
using ItemTradeApp.Features.Users.Auth.DTOs.ResponseDtos;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration.DTOs.Response;
using ItemTradeApp.Features.Users.Shared.DTOs;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.Users.Auth;

[ApiController]
[Route("[controller]")]
public class AuthController(IAuthService authService, IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult Csrf()
    {
        IssueAntiforgeryToken();
        return NoContent();
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<Result<AuthZeroBodyResponse>>> Register([FromBody] RegisterRequest? req)
    {
        if (req is null)
            return Result<AuthZeroBodyResponse>.BadRequest("Body is required.").ToActionResult();

        var result = await authService.RegisterAsync(req);
        return result.ToActionResult();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<Result<LoginResponse>>> Login([FromBody] LoginRequest? req)
    {
        if (req is null)
            return Result<LoginResponse>.BadRequest("Body is required.").ToActionResult();

        var result = await authService.LoginAsync(req);
        if (!result.IsSuccess || result.Data is null)
            return result.ToActionResult();

        var ok = result.Data;

        if (!string.IsNullOrWhiteSpace(ok.RefreshToken))
            SetRefreshCookie(ok.RefreshToken!);

        SetAccessCookie(ok.AccessToken!, ok.ExpiresIn);
        IssueAntiforgeryToken();

        var dto = new LoginResponse(ok.Id, ok.ExpiresIn);
        return Result<LoginResponse>.Success(dto).ToActionResult();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<Result<AuthZeroBodyResponse>>> ForgotPassword([FromBody] ForgotPasswordRequest? req)
    {
        if (req is null)
            return Result<AuthZeroBodyResponse>.BadRequest("Body is required.").ToActionResult();

        var result = await authService.ForgotPasswordAsync(req);
        return result.ToActionResult();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<Result<RefreshResponse>>> Refresh()
    {
        var rt = Request.Cookies[RefreshCookieName];

        if (string.IsNullOrWhiteSpace(rt))
            return Result<RefreshResponse>.Unauthorized("Missing refresh token cookie.").ToActionResult();

        var result = await authService.RefreshAsync(rt);
        if (!result.IsSuccess || result.Data is null)
            return result.ToActionResult();

        var ok = result.Data;

        if (!string.IsNullOrWhiteSpace(ok.RefreshToken))
            SetRefreshCookie(ok.RefreshToken!);

        SetAccessCookie(ok.AccessToken!, ok.ExpiresIn);
        IssueAntiforgeryToken();

        var dto = new RefreshResponse(ok.Id, ok.ExpiresIn);
        return Result<RefreshResponse>.Success(dto).ToActionResult();
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var rt = Request.Cookies[RefreshCookieName];

        if (!string.IsNullOrWhiteSpace(rt))
            await authService.LogoutAsync(rt);

        DeleteRefreshCookie();
        DeleteAccessCookie();

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<Result<AuthMeDTO>>> Me()
    {
        IssueAntiforgeryToken();

        var login =
            User.FindFirst("login")?.Value ??
            User.FindFirst(ClaimTypes.Name)?.Value ??
            User.FindFirst("name")?.Value ??
            User.FindFirst("preferred_username")?.Value ??
            User.FindFirst(ClaimTypes.Email)?.Value;

        var auth0UserId = Auth0IdHandler.GetUserId(User);

        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<AuthMeDTO>.Unauthorized("Missing sub claim in JWT.").ToActionResult();

        var roles = User.FindAll("https://inzynierka.com/roles")
            .Select(x => x.Value)
            .Distinct()
            .ToList();

        var userId = await authService.GetUserIdAsync(auth0UserId);

        if (!userId.IsSuccess)
            return Result<AuthMeDTO>.Unauthorized("User not found.").ToActionResult();

        var dto = new AuthMeDTO(userId.Data, true, login, roles);
        return Result<AuthMeDTO>.Success(dto).ToActionResult();
    }

    private const string RefreshCookieName = "rt";
    private const string AccessCookieName = "at";

    private CookieOptions BaseHttpOnlyCookie(string path) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.None,
        Path = path
    };

    private void SetRefreshCookie(string refreshToken)
    {
        var opts = BaseHttpOnlyCookie("/");
        Response.Cookies.Append(RefreshCookieName, refreshToken, opts);
    }

    private void DeleteRefreshCookie()
    {
        var opts = BaseHttpOnlyCookie("/");
        Response.Cookies.Delete(RefreshCookieName, opts);
    }

    private void SetAccessCookie(string accessToken, int expiresInSeconds)
    {
        var opts = BaseHttpOnlyCookie("/");
        opts.Expires = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);

        Response.Cookies.Append(AccessCookieName, accessToken, opts);
    }

    private void DeleteAccessCookie()
    {
        var opts = BaseHttpOnlyCookie("/");
        Response.Cookies.Delete(AccessCookieName, opts);
    }

    private void IssueAntiforgeryToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);

        if (!string.IsNullOrWhiteSpace(tokens.RequestToken))
            Response.Headers["X-XSRF-TOKEN"] = tokens.RequestToken!;
    }
}