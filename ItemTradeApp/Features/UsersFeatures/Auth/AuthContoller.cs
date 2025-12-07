
using ItemTradeApp.AuthZeroCommunication.Dto.ResponseDtos;
using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.UsersFeature.Auth.Dto.RequestDtos;
using ItemTradeApp.LoginFeature.Dto.RequestDtos;
using ItemTradeApp.AuthZeroCommunication.Mappers;
using ItemTradeApp.LoginFeature.Dto.ResponseDtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.LoginFeature;


[ApiController]
[Route("[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<Result<AuthZeroBodyResponse>>> Register([FromBody] RegisterRequest? req)
    {
        if (req is null)
        {
            var error = Result<AuthZeroBodyResponse>.BadRequest("Body is required.");
            return error.ToActionResult();        }

        if (string.IsNullOrWhiteSpace(req.Email))
            ModelState.AddModelError(nameof(req.Email), "Email is required.");
        if (string.IsNullOrWhiteSpace(req.Password))
            ModelState.AddModelError(nameof(req.Password), "Password is required.");
        if (!ModelState.IsValid)
        {
            var msg = string.Join(" | ",
                ModelState.SelectMany(kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage)));
            var error = Result<AuthZeroBodyResponse>.BadRequest(msg);
            return error.ToActionResult();
        }

        var result = await authService.RegisterAsync(req);
        return result.ToActionResult();
    }

    [HttpPost("login")]
    public async Task<ActionResult<Result<LoginResponse>>> Login([FromBody] LoginRequest? req)
    {
        if (req is null)
        {
            var error = Result<LoginResponse>.BadRequest("Body is required.");
            return error.ToActionResult();
        }

        if (string.IsNullOrWhiteSpace(req.Email))
            ModelState.AddModelError(nameof(req.Email), "Email is required.");
        if (string.IsNullOrWhiteSpace(req.Password))
            ModelState.AddModelError(nameof(req.Password), "Password is required.");
        if (!ModelState.IsValid)
        {
            var msg = string.Join(" | ",
                ModelState.SelectMany(kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage)));
            var error = Result<LoginResponse>.BadRequest(msg);
            return error.ToActionResult();
        }
        var result = await authService.LoginAsync(req);
        if (!result.IsSuccess || result.Data is null)
        {
            return result.ToActionResult();
        }
        var resultData = result.Data;
        if (!string.IsNullOrWhiteSpace(resultData.RefreshToken))
        {
            SetRefreshCookie(resultData.RefreshToken!);
        }
        // not sending a refreshToken in response
        var dto = new LoginResponse(
            resultData.Id,
            resultData.AccessToken,
            resultData.ExpiresIn,
            resultData.IdToken
        );

        var success = Result<LoginResponse>.Success(dto);
        return success.ToActionResult();
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<Result<AuthZeroBodyResponse>>> ForgotPassword([FromBody] ForgotPasswordRequest? req)
    {
        if (req is null)
        {
            var error = Result<AuthZeroBodyResponse>.BadRequest("Body is required.");
            return error.ToActionResult();
        }

        if (string.IsNullOrWhiteSpace(req.Email))
            ModelState.AddModelError(nameof(req.Email), "Email is required.");
        if (!ModelState.IsValid)
        {
            var msg = string.Join(" | ",
                ModelState.SelectMany(kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage)));
            var error = Result<AuthZeroBodyResponse>.BadRequest(msg);
            return error.ToActionResult();
        }

        var result = await authService.ForgotPasswordAsync(req);
        return result.ToActionResult();
    }

    [HttpPost("refresh")]
    [Authorize]
    public async Task<ActionResult<Result<RefreshResponse>>> Refresh()
    {
        var rt = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(rt))
        {
            var error = Result<RefreshResponse>.Unauthorized("Missing refresh token cookie.");
            return error.ToActionResult();
        }

        var result = await authService.RefreshAsync(rt);

        if (!result.IsSuccess || result.Data is null)
        {
            return result.ToActionResult();
        }

        var ok = result.Data;

        if (!string.IsNullOrWhiteSpace(ok.RefreshToken))
        {
            SetRefreshCookie(ok.RefreshToken!);
        }

        var dto = new RefreshResponse(
            ok.AccessToken,
            ok.ExpiresIn,
            ok.IdToken
        );

        var success = Result<RefreshResponse>.Success(dto);
        return success.ToActionResult();
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var rt = Request.Cookies[RefreshCookieName];

        if (!string.IsNullOrWhiteSpace(rt))
        {
            var result = await authService.LogoutAsync(rt);
            DeleteRefreshCookie();

            if (!result.IsSuccess)
            {
                return StatusCode((int)result.Status, result.Message);
            }

            return NoContent();
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

