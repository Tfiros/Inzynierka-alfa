
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

        if (string.IsNullOrWhiteSpace(req.Username))
            ModelState.AddModelError(nameof(req.Username), "Username is required.");
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
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest? req)
    {
        if (req is null)
        {
            ModelState.AddModelError(string.Empty, "Body is required.");
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            ModelState.AddModelError(nameof(req.RefreshToken), "RefreshToken is required.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await loginService.RefreshAsync(req);
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
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? req)
    {
        var refreshFromCookie = Request.Cookies[RefreshCookieName];
        var refresh = req?.RefreshToken ?? refreshFromCookie;

        if (string.IsNullOrWhiteSpace(refresh))
        {
            ModelState.AddModelError(nameof(req.RefreshToken), "RefreshToken is required (cookie or body).");
            return ValidationProblem(ModelState);
        }

        var result = await loginService.LogoutAsync(new LogoutRequest(refresh));
        DeleteRefreshCookie();

        return result.Matching(
            _   => StatusCode(StatusCodes.Status204NoContent),
            err => StatusCode(err.StatusCode, Auth0DetailsMapper.Build("auth0_revoke_failed", err.Body))
        );
    }


    #region HELPERS

    private const string RefreshCookieName = "rt";

    private CookieOptions BuildRefreshCookieOptions()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // tylko HTTPS
            SameSite = SameSiteMode.Lax, // Lax dla SPA pod tym samym hostem; dla cross-site: None
            Path = "/", // dostępne w całej aplikacji
            Expires = DateTimeOffset.UtcNow.AddDays(14),
            // Domain = ".naszadomena.pl",
        };
    }

    private void SetRefreshCookie(string refreshToken)
    {
        Response.Cookies.Append(RefreshCookieName, refreshToken, BuildRefreshCookieOptions());
    }

    private void DeleteRefreshCookie()
    {
        // Path musi być takie samo jak przy ustawieniu
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = "/" });
    }
    #endregion
}

