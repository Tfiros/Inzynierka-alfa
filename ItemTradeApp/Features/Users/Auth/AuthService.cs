using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Shared;
using ItemTradeApp.Features.Users.Auth.DTOs.RequestDtos;
using ItemTradeApp.Features.Users.Auth.DTOs.ResponseDtos;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration.DTOs.Response;
using ItemTradeApp.Features.Users.Shared.DTOs;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.Features.Users.Auth;

public interface IAuthService
{
    Task<Result<AuthZeroBodyResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default);
    Task<Result<LoginResponse>> LoginAsync(LoginRequest req, CancellationToken ct = default);
    Task<Result<AuthZeroBodyResponse>> ForgotPasswordAsync(ForgotPasswordRequest req, CancellationToken ct = default);
    Task<Result<RefreshResponse>> RefreshAsync(string req, CancellationToken ct = default);
    Task<Result<AuthZeroBodyResponse>> LogoutAsync(string req, CancellationToken ct = default);
    Task<Result<int>> GetUserIdAsync(string req, CancellationToken ct = default);
}

public class AuthService(
    IOptions<AuthZeroOptions> config,
    IAuthZeroAPIClient apiClient,
    IAuthRepository authRepository,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly AuthZeroOptions _config = config.Value;

    public async Task<Result<AuthZeroBodyResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var res = await apiClient.SignupAsync(
            email: req.Email,
            password: req.Password,
            connection: _config.Realm,
            clientId: _config.ClientId,
            name: req.Username,
            ct);

        if (!res.IsSuccess || res.Data?.Details.RawResponse is null)
        {
            logger.LogWarning(
                "Registration in Auth0 failed. Email: {Email}, Status: {Status}, ProviderError: {ProviderError}",
                req.Email,
                res.Status,
                GetProviderError(res));

            return res.Status switch
            {
                ResultStatus.Conflict =>
                    Result<AuthZeroBodyResponse>.Conflict("User with this email already exists."),

                ResultStatus.BadRequest =>
                    Result<AuthZeroBodyResponse>.BadRequest("Registration failed. User with such email already exists."),

                ResultStatus.Unauthorized =>
                    Result<AuthZeroBodyResponse>.Unauthorized("Registration failed."),

                ResultStatus.NotFound =>
                    Result<AuthZeroBodyResponse>.NotFound("Registration service unavailable."),

                ResultStatus.InternalServerError =>
                    Result<AuthZeroBodyResponse>.InternalServerError("Registration service unavailable."),

                _ =>
                    Result<AuthZeroBodyResponse>.BadRequest("Registration failed.")
            };
        }

        string auth0Id;

        try
        {
            using var doc = JsonDocument.Parse(res.Data.Details.RawResponse);

            auth0Id = doc.RootElement.TryGetProperty("_id", out var idElement)
                ? idElement.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid Auth0 registration response. Email: {Email}", req.Email);
            return Result<AuthZeroBodyResponse>.InternalServerError("Registration response was invalid.");
        }

        if (string.IsNullOrWhiteSpace(auth0Id))
        {
            logger.LogError("Auth0 registration response did not contain _id. Email: {Email}", req.Email);
            return Result<AuthZeroBodyResponse>.InternalServerError("Registration response was invalid.");
        }

        await authRepository.Register(req, auth0Id);

        res.Message = "Registration successful";
        return res;
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var res = await apiClient.PasswordRealmTokenAsync(
            username: req.Email,
            password: req.Password,
            realm: _config.Realm,
            clientId: _config.ClientId,
            clientSecret: _config.ClientSecret,
            audience: _config.Audience,
            scope: "openid profile email offline_access",
            ct);

        if (!res.IsSuccess || res.Data?.Details.RawResponse is null)
        {
            logger.LogWarning(
                "Auth0 login failed. Email: {Email}, Status: {Status}, ProviderError: {ProviderError}",
                req.Email,
                res.Status,
                GetProviderError(res));

            return res.Status switch
            {
                ResultStatus.BadRequest =>
                    Result<LoginResponse>.BadRequest("Invalid email or password."),

                ResultStatus.Unauthorized =>
                    Result<LoginResponse>.Unauthorized("Invalid email or password."),

                ResultStatus.NotFound =>
                    Result<LoginResponse>.NotFound("Invalid email or password."),

                ResultStatus.Conflict =>
                    Result<LoginResponse>.Conflict("Login failed."),

                ResultStatus.InternalServerError =>
                    Result<LoginResponse>.InternalServerError("Login service unavailable. Check if you have verified your email."),

                _ =>
                    Result<LoginResponse>.BadRequest("Login failed.")
            };
        }

        TokenPayload tokenPayload;

        try
        {
            tokenPayload = ParseTokenPayload(res.Data.Details.RawResponse);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid Auth0 login response. Email: {Email}", req.Email);
            return Result<LoginResponse>.InternalServerError("Login response was invalid.");
        }

        if (string.IsNullOrWhiteSpace(tokenPayload.AccessToken))
            return Result<LoginResponse>.InternalServerError("Login response was invalid.");

        var user = await authRepository.GetUserByEmail(req.Email);

        if (user is null)
        {
            logger.LogWarning("User logged in through Auth0 but does not exist in local database. Email: {Email}", req.Email);
            return Result<LoginResponse>.NotFound("User account was not found.");
        }

        var dto = new LoginResponse(
            user.ID,
            tokenPayload.ExpiresIn,
            tokenPayload.IdToken,
            tokenPayload.AccessToken,
            tokenPayload.RefreshToken);

        return Result<LoginResponse>.Success(dto, "Login successful");
    }

    public async Task<Result<AuthZeroBodyResponse>> ForgotPasswordAsync(ForgotPasswordRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var res = await apiClient.ChangePasswordAsync(
            email: req.Email,
            connection: _config.Realm,
            clientId: _config.ClientId,
            ct);

        if (!res.IsSuccess)
        {
            logger.LogWarning(
                "Auth0 forgot password failed. Email: {Email}, Status: {Status}, ProviderError: {ProviderError}",
                req.Email,
                res.Status,
                GetProviderError(res));

            return res.Status switch
            {
                ResultStatus.BadRequest =>
                    Result<AuthZeroBodyResponse>.BadRequest("Password reset failed."),

                ResultStatus.Unauthorized =>
                    Result<AuthZeroBodyResponse>.Unauthorized("Password reset failed."),

                ResultStatus.NotFound =>
                    Result<AuthZeroBodyResponse>.NotFound("Password reset failed."),

                ResultStatus.Conflict =>
                    Result<AuthZeroBodyResponse>.Conflict("Password reset failed."),

                ResultStatus.InternalServerError =>
                    Result<AuthZeroBodyResponse>.InternalServerError("Password reset service unavailable."),

                _ =>
                    Result<AuthZeroBodyResponse>.BadRequest("Password reset failed.")
            };
        }

        if (res.Data is not null)
            res.Data.Message = "Reset email sent";

        res.Message = "Reset email sent";
        return res;
    }

    public async Task<Result<RefreshResponse>> RefreshAsync(string req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req))
            return Result<RefreshResponse>.Unauthorized("Missing refresh token.");

        var res = await apiClient.RefreshTokenAsync(
            refreshToken: req,
            clientId: _config.ClientId,
            clientSecret: _config.ClientSecret,
            scope: null,
            ct);

        if (!res.IsSuccess || res.Data?.Details.RawResponse is null)
        {
            logger.LogWarning(
                "Auth0 refresh failed. Status: {Status}, ProviderError: {ProviderError}",
                res.Status,
                GetProviderError(res));

            return res.Status switch
            {
                ResultStatus.BadRequest =>
                    Result<RefreshResponse>.Unauthorized("Session expired."),

                ResultStatus.Unauthorized =>
                    Result<RefreshResponse>.Unauthorized("Session expired."),

                ResultStatus.NotFound =>
                    Result<RefreshResponse>.Unauthorized("Session expired."),

                ResultStatus.Conflict =>
                    Result<RefreshResponse>.Unauthorized("Session expired."),

                ResultStatus.InternalServerError =>
                    Result<RefreshResponse>.InternalServerError("Refresh service unavailable."),

                _ =>
                    Result<RefreshResponse>.Unauthorized("Session expired.")
            };
        }

        TokenPayload tokenPayload;

        try
        {
            tokenPayload = ParseTokenPayload(res.Data.Details.RawResponse);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid Auth0 refresh response.");
            return Result<RefreshResponse>.InternalServerError("Refresh response was invalid.");
        }

        if (string.IsNullOrWhiteSpace(tokenPayload.AccessToken))
            return Result<RefreshResponse>.InternalServerError("Refresh response was invalid.");

        string? auth0Id;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(tokenPayload.AccessToken);
            auth0Id = token.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not read access token during refresh.");
            return Result<RefreshResponse>.InternalServerError("Refresh response was invalid.");
        }

        if (string.IsNullOrWhiteSpace(auth0Id))
            return Result<RefreshResponse>.InternalServerError("Refresh response was invalid.");

        var trimmedAuth0UserId = Auth0IdHandler.Trim(auth0Id);

        var user = await authRepository.GetUserByAuth0Id(trimmedAuth0UserId);

        if (user is null)
        {
            logger.LogWarning("Auth0 refreshed token but local user was not found. Auth0UserId: {Auth0UserId}", trimmedAuth0UserId);
            return Result<RefreshResponse>.NotFound("User account was not found.");
        }

        var dto = new RefreshResponse(
            user.ID,
            tokenPayload.ExpiresIn,
            tokenPayload.IdToken,
            tokenPayload.AccessToken,
            tokenPayload.RefreshToken);

        return Result<RefreshResponse>.Success(dto, "Refresh successful");
    }

    public async Task<Result<AuthZeroBodyResponse>> LogoutAsync(string req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req))
            return Result<AuthZeroBodyResponse>.BadRequest("Missing refresh token.");

        var res = await apiClient.RevokeRefreshTokenAsync(
            refreshToken: req,
            clientId: _config.ClientId,
            clientSecret: _config.ClientSecret,
            ct);

        if (!res.IsSuccess)
        {
            logger.LogWarning(
                "Auth0 logout failed. Status: {Status}, ProviderError: {ProviderError}",
                res.Status,
                GetProviderError(res));

            return res.Status switch
            {
                ResultStatus.BadRequest =>
                    Result<AuthZeroBodyResponse>.BadRequest("Logout failed."),

                ResultStatus.Unauthorized =>
                    Result<AuthZeroBodyResponse>.Unauthorized("Logout failed."),

                ResultStatus.NotFound =>
                    Result<AuthZeroBodyResponse>.NotFound("Logout failed."),

                ResultStatus.Conflict =>
                    Result<AuthZeroBodyResponse>.Conflict("Logout failed."),

                ResultStatus.InternalServerError =>
                    Result<AuthZeroBodyResponse>.InternalServerError("Logout service unavailable."),

                _ =>
                    Result<AuthZeroBodyResponse>.BadRequest("Logout failed.")
            };
        }

        if (res.Data is not null)
            res.Data.Message = "Logout successful";

        res.Message = "Logout successful";
        return res;
    }

    public async Task<Result<int>> GetUserIdAsync(string authZeroId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(authZeroId))
            return Result<int>.BadRequest("Auth0 user id is required.");

        var trimmedAuth0UserId = Auth0IdHandler.Trim(authZeroId);

        var user = await authRepository.GetUserByAuth0Id(trimmedAuth0UserId);

        if (user is null)
            return Result<int>.NotFound("User not found.");

        return Result<int>.Success(user.ID, "User id retrieved successfully");
    }

    private static TokenPayload ParseTokenPayload(string rawResponse)
    {
        using var doc = JsonDocument.Parse(rawResponse);
        var root = doc.RootElement;

        var accessToken = root.TryGetProperty("access_token", out var accessTokenElement)
            ? accessTokenElement.GetString()
            : null;

        var expiresIn = root.TryGetProperty("expires_in", out var expiresInElement)
            ? expiresInElement.GetInt32()
            : 0;

        var refreshToken = root.TryGetProperty("refresh_token", out var refreshTokenElement)
            ? refreshTokenElement.GetString()
            : null;

        var idToken = root.TryGetProperty("id_token", out var idTokenElement)
            ? idTokenElement.GetString()
            : null;

        return new TokenPayload(accessToken, expiresIn, refreshToken, idToken);
    }
    private static string? GetProviderError(Result<AuthZeroBodyResponse> result)
    {
        return result.Data?.Details.ErrorDescription
               ?? result.Data?.Details.Error
               ?? result.Data?.Details.Text
               ?? result.Message;
    }

    private sealed record TokenPayload(
        string? AccessToken,
        int ExpiresIn,
        string? RefreshToken,
        string? IdToken);
}