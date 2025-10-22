
using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.UsersFeature.Auth.Dto.RequestDtos;
using ItemTradeApp.LoginFeature.Dto.RequestDtos;
using ItemTradeApp.LoginFeature.Mappers;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.LoginFeature;


[ApiController]
[Route("[controller]")]
public class LoginController(IAuthService loginService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest? req)
    {
        if (req is null)
        {
            ModelState.AddModelError(string.Empty, "Body is required.");
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(req.Email))
            ModelState.AddModelError(nameof(req.Email), "Email is required.");
        if (string.IsNullOrWhiteSpace(req.Password))
            ModelState.AddModelError(nameof(req.Password), "Password is required.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await loginService.RegisterAsync(req);
        return result.Matching(
            ok => StatusCode(StatusCodes.Status201Created, ok),
            err => StatusCode(err.StatusCode, Auth0DetailsMapper.Build("auth0_signup_failed", err.Body))
        );
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginRequest? req)
    {
        if (req is null)
        {
            ModelState.AddModelError(string.Empty, "Body is required.");
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(req.Email))
            ModelState.AddModelError(nameof(req.Email), "Username is required.");
        if (string.IsNullOrWhiteSpace(req.Password))
            ModelState.AddModelError(nameof(req.Password), "Password is required.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await loginService.LoginAsync(req);
        return result.Matching(
            ok =>
            {
                if (!string.IsNullOrWhiteSpace(ok.RefreshToken))
                    SetRefreshCookie(ok.RefreshToken!);
                return Ok(new { ok.AccessToken, ok.ExpiresIn, ok.IdToken });
            },
            err => StatusCode(err.StatusCode, Auth0DetailsMapper.Build("auth0_token_failed", err.Body))
        );
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest? req)
    {
        if (req is null)
        {
            ModelState.AddModelError(string.Empty, "Body is required.");
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(req.Email))
            ModelState.AddModelError(nameof(req.Email), "Email is required.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await loginService.ForgotPasswordAsync(req);
        return result.Matching(
            ok => Ok(ok),
            err => StatusCode(err.StatusCode, Auth0DetailsMapper.Build("auth0_change_password_failed", err.Body))
        );
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var rt = Request.Cookies["rt"];
        if (string.IsNullOrEmpty(rt))
            return Unauthorized();

        var result = await loginService.RefreshAsync(rt);
        return result.Matching(
            ok =>
            {
                if (!string.IsNullOrWhiteSpace(ok.RefreshToken))
                    SetRefreshCookie(ok.RefreshToken!);
                return Ok(new { ok.AccessToken, ok.ExpiresIn, ok.IdToken });
            },
            err => StatusCode(err.StatusCode, Auth0DetailsMapper.Build("auth0_refresh_failed", err.Body))
        );
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var rt = Request.Cookies[RefreshCookieName];

        if (!string.IsNullOrWhiteSpace(rt))
        {
            var result = await loginService.LogoutAsync(rt);
            DeleteRefreshCookie();

            return result.Matching(
                _   => NoContent(), // 204
                err => StatusCode(err.StatusCode, new { message = "revoke_failed", details = err.Body })
            );
        }
        DeleteRefreshCookie();
        return NoContent();
    }


    #region HELPERS

    private const string RefreshCookieName = "rt";
    private void SetRefreshCookie(string refreshToken) =>
        SetRefreshCookie(refreshToken, DateTimeOffset.UtcNow.AddDays(7));
    private void SetRefreshCookie(string refreshToken, DateTimeOffset? expiresUtc)
    {
        var opts = new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Strict,
            Path     = "/",
            Expires  = expiresUtc 
            // Domain = ".twojadomena.pl"
        };
        Response.Cookies.Append(RefreshCookieName, refreshToken, opts);
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Append(
            RefreshCookieName,
            "",
            new CookieOptions
            {
                HttpOnly = true,
                Secure   = true,
                SameSite = SameSiteMode.Strict,
                Path     = "/",
                Expires  = DateTimeOffset.UnixEpoch
            });
    }
    #endregion
}

