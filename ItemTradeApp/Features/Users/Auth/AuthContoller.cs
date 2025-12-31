using System.Security.Claims;
using ItemTradeApp.AuthZeroCommunication.Dto.Response;
using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Users.Auth.DTOs.RequestDtos;
using ItemTradeApp.Users.Auth.DTOs.ResponseDtos;
using ItemTradeApp.Users.Shared.DTOs;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Users.Auth;

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

        if (string.IsNullOrWhiteSpace(req.Email))
            ModelState.AddModelError(nameof(req.Email), "Email is required.");
        if (string.IsNullOrWhiteSpace(req.Password))
            ModelState.AddModelError(nameof(req.Password), "Password is required.");

        if (!ModelState.IsValid)
        {
            var msg = string.Join(" | ",
                ModelState.SelectMany(kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage)));
            return Result<AuthZeroBodyResponse>.BadRequest(msg).ToActionResult();
        }

        var result = await authService.RegisterAsync(req);
        return result.ToActionResult();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<Result<LoginResponse>>> Login([FromBody] LoginRequest? req)
    {
        if (req is null)
            return Result<LoginResponse>.BadRequest("Body is required.").ToActionResult();

        if (string.IsNullOrWhiteSpace(req.Email))
            ModelState.AddModelError(nameof(req.Email), "Email is required.");
        if (string.IsNullOrWhiteSpace(req.Password))
            ModelState.AddModelError(nameof(req.Password), "Password is required.");

        if (!ModelState.IsValid)
        {
            var msg = string.Join(" | ",
                ModelState.SelectMany(kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage)));
            return Result<LoginResponse>.BadRequest(msg).ToActionResult();
        }

        var result = await authService.LoginAsync(req);
        if (!result.IsSuccess || result.Data is null)
            return result.ToActionResult();

        var ok = result.Data;

        if (!string.IsNullOrWhiteSpace(ok.RefreshToken))
            SetRefreshCookie(ok.RefreshToken!, DateTimeOffset.UtcNow.AddDays(7));

        SetAccessCookie(ok.AccessToken!, ok.ExpiresIn);

        // ważne: po zalogowaniu wydaj token antiforgery powiązany z aktualnym userem
        IssueAntiforgeryToken();

        var dto = new LoginResponse(ok.Id, ok.ExpiresIn, ok.IdToken);
        return Result<LoginResponse>.Success(dto).ToActionResult();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<Result<AuthZeroBodyResponse>>> ForgotPassword([FromBody] ForgotPasswordRequest? req)
    {
        if (req is null)
            return Result<AuthZeroBodyResponse>.BadRequest("Body is required.").ToActionResult();

        if (string.IsNullOrWhiteSpace(req.Email))
            ModelState.AddModelError(nameof(req.Email), "Email is required.");

        if (!ModelState.IsValid)
        {
            var msg = string.Join(" | ",
                ModelState.SelectMany(kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage)));
            return Result<AuthZeroBodyResponse>.BadRequest(msg).ToActionResult();
        }

        var result = await authService.ForgotPasswordAsync(req);
        return result.ToActionResult();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<Result<RefreshResponse>>> Refresh()
    {
        var rt = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(rt))
            return Result<RefreshResponse>.Unauthorized("Missing refresh token cookie.").ToActionResult();

        var result = await authService.RefreshAsync(rt);
        if (!result.IsSuccess || result.Data is null)
            return result.ToActionResult();

        var ok = result.Data;

        if (!string.IsNullOrWhiteSpace(ok.RefreshToken))
            SetRefreshCookie(ok.RefreshToken!, DateTimeOffset.UtcNow.AddDays(7));

        SetAccessCookie(ok.AccessToken!, ok.ExpiresIn);

        // ważne: po refresh też odśwież token antiforgery (może być per-user)
        IssueAntiforgeryToken();

        var dto = new RefreshResponse(ok.Id, ok.ExpiresIn, ok.IdToken);
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
    public ActionResult<Result<AuthMeDTO>> Me()
    {
        // opcjonalnie, ale pomaga: odnów token przy /me
        IssueAntiforgeryToken();

        var login =
            User.FindFirst("login")?.Value ??
            User.FindFirst(ClaimTypes.Name)?.Value ??
            User.FindFirst("name")?.Value ??
            User.FindFirst("preferred_username")?.Value ??
            User.FindFirst(ClaimTypes.Email)?.Value;

        var roles = User.FindAll("https://inzynierka.com/roles")
            .Select(x => x.Value)
            .Distinct()
            .ToList();

        // Jeśli chcesz userId, to zwykle masz go w "sub" (auth0 id) – ale DTO masz na int.
        // Zakładam, że authService/me mapuje to już gdzie indziej, więc tu zostaje jak było.
        var dto = new AuthMeDTO(true, login, roles);
        return Result<AuthMeDTO>.Success(dto).ToActionResult();
    }

    #region HELPERS

    private const string RefreshCookieName = "rt";
    private const string AccessCookieName  = "at";

    private bool IsDev => HttpContext.RequestServices
        .GetRequiredService<IHostEnvironment>()
        .IsDevelopment();

    private CookieOptions BaseHttpOnlyCookie(string path, DateTimeOffset? expiresUtc) => new()
    {
        HttpOnly = true,
        Secure   = true,              // masz SPA na https => OK
        SameSite = SameSiteMode.None, // cross-site cookie
        Path     = path,
        Expires  = expiresUtc
    };

    private void SetRefreshCookie(string refreshToken, DateTimeOffset expiresUtc)
    {
        // refresh cookie tylko na endpoint refresh
        var opts = BaseHttpOnlyCookie("/Auth/refresh", expiresUtc);
        Response.Cookies.Append(RefreshCookieName, refreshToken, opts);
    }

    private void DeleteRefreshCookie()
    {
        var opts = BaseHttpOnlyCookie("/Auth/refresh", DateTimeOffset.UnixEpoch);
        Response.Cookies.Append(RefreshCookieName, "", opts);
    }

    private void SetAccessCookie(string accessToken, int expiresInSeconds)
    {
        var opts = BaseHttpOnlyCookie("/", DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds));
        Response.Cookies.Append(AccessCookieName, accessToken, opts);
    }

    private void DeleteAccessCookie()
    {
        var opts = BaseHttpOnlyCookie("/", DateTimeOffset.UnixEpoch);
        Response.Cookies.Append(AccessCookieName, "", opts);
    }

    /// <summary>
    /// Wydaje i zapisuje cookie antiforgery (.AspNetCore.Antiforgery.*) oraz wystawia request token w headerze.
    /// Front powinien brać X-XSRF-TOKEN z response header i wysyłać w request header dla POST/PUT/PATCH/DELETE.
    /// </summary>
    private void IssueAntiforgeryToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);

        if (!string.IsNullOrWhiteSpace(tokens.RequestToken))
        {
            // Uwaga: w AddAntiforgery ustawiasz HeaderName = "X-XSRF-TOKEN"
            Response.Headers["X-XSRF-TOKEN"] = tokens.RequestToken!;
        }
    }

    #endregion
}
