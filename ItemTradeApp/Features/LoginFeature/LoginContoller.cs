// src/ItemTradeApp/LoginFeature/LoginController.cs

using System.Text;
using System.Text.Json;
using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.LoginFeature.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.LoginFeature;

public record LoginRequest(string Username, string Password);

[ApiController]
[Route("[controller]")]
public class LoginController(ILoginService loginService) : ControllerBase
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
            ok => Ok(ok),
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
            ok => Ok(ok),
            err => StatusCode(err.StatusCode, Auth0DetailsMapper.Build("auth0_refresh_failed", err.Body))
        );
    }
}

