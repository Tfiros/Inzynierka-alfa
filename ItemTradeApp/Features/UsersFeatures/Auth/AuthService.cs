using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using ItemTradeApp.AuthZeroCommunication;
using ItemTradeApp.AuthZeroCommunication.Dto.ResponseDtos;
using ItemTradeApp.AuthZeroCommunication.Mappers;
using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.LoginFeature.Dto;
using ItemTradeApp.LoginFeature.Dto.RequestDtos;
using ItemTradeApp.LoginFeature.Dto.ResponseDtos;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.LoginFeature;

public interface IAuthService
{
    Task<Result<AuthZeroBodyResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default);
    Task<Result<LoginResponse>>       LoginAsync(LoginRequest req, CancellationToken ct = default);
    Task<Result<AuthZeroBodyResponse>> ForgotPasswordAsync(ForgotPasswordRequest req, CancellationToken ct = default);
    Task<Result<RefreshResponse>>     RefreshAsync(string req, CancellationToken ct = default);
    Task<Result<AuthZeroBodyResponse>> LogoutAsync(string req, CancellationToken ct = default);
}

public class AuthService(
    IOptions<AuthZeroOptions> config,
    IAuthZeroAPIClient apiClient,
    IAuthRepository authRepository) : IAuthService
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

        if (!res.IsSuccess)
        {
            var msg = "Registration failed! There is a user alredy with such credentials.";
            return res.Status switch
            {
                ResultStatus.BadRequest      => Result<AuthZeroBodyResponse>.BadRequest(msg),
                ResultStatus.Unauthorized    => Result<AuthZeroBodyResponse>.Unauthorized(msg),
                ResultStatus.Conflict        => Result<AuthZeroBodyResponse>.Conflict(msg),
                ResultStatus.NotFound        => Result<AuthZeroBodyResponse>.NotFound(msg),
                ResultStatus.InternalServerError => Result<AuthZeroBodyResponse>.InternalServerError(msg),
                _ => Result<AuthZeroBodyResponse>.BadRequest(msg)
            };
        }

        using var doc = JsonDocument.Parse(res.Data.Details.RawResponse);
        var auth0Id = doc.RootElement.GetProperty("_id").GetString() ?? string.Empty;

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

        if (!res.IsSuccess)
        {
            res.Message = "Login failed due to error: " + res.Data.Details.ErrorDescription;
            return res.Status switch
            {
                ResultStatus.BadRequest      => Result<LoginResponse>.BadRequest(res.Message),
                ResultStatus.Unauthorized    => Result<LoginResponse>.Unauthorized(res.Message),
                ResultStatus.NotFound        => Result<LoginResponse>.NotFound(res.Message),
                ResultStatus.Conflict        => Result<LoginResponse>.Conflict(res.Message),
                ResultStatus.InternalServerError => Result<LoginResponse>.InternalServerError(res.Message),
                _ => Result<LoginResponse>.BadRequest(res.Message)
            };
        }

        using var doc = JsonDocument.Parse(res.Data.Details.RawResponse);
        var accessToken  = doc.RootElement.GetProperty("access_token").GetString();
        var expiresIn    = doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 0;
        var refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        var idToken      = doc.RootElement.TryGetProperty("id_token", out var idt) ? idt.GetString() : null;
        
        if (string.IsNullOrEmpty(accessToken))
        {
            return Result<LoginResponse>.InternalServerError("no_access_token_from_auth0");
        }

        var user = await authRepository.GetUserByEmail(req.Email);
        if (user is null)
        {
            return Result<LoginResponse>.NotFound("user_not_found_in_local_db");
        }

        var dto = new LoginResponse(
            user.ID,
            accessToken!,
            expiresIn,
            refreshToken,
            idToken);

        return Result<LoginResponse>.Success(dto, "Login Successful");
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
            var msg = "Reseting password failed due to: " + res.Data.Details.ErrorDescription;
            return res.Status switch
            {
                ResultStatus.BadRequest      => Result<AuthZeroBodyResponse>.BadRequest(msg),
                ResultStatus.Unauthorized    => Result<AuthZeroBodyResponse>.Unauthorized(msg),
                ResultStatus.NotFound        => Result<AuthZeroBodyResponse>.NotFound(msg),
                ResultStatus.Conflict        => Result<AuthZeroBodyResponse>.Conflict(msg),
                ResultStatus.InternalServerError => Result<AuthZeroBodyResponse>.InternalServerError(msg),
                _ => Result<AuthZeroBodyResponse>.BadRequest(msg)
            };
        }
        res.Data.Message = "Reset email sent";

        return res;
    }

    public async Task<Result<RefreshResponse>> RefreshAsync(string req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var res = await apiClient.RefreshTokenAsync(
            refreshToken: req,
            clientId: _config.ClientId,
            clientSecret: _config.ClientSecret,
            scope: null,
            ct);

        if (!res.IsSuccess || res.Data is null)
        {
            var msg = "Refresh failed due to: " + res.Data.Details.ErrorDescription;
            return res.Status switch
            {
                ResultStatus.BadRequest      => Result<RefreshResponse>.BadRequest(msg),
                ResultStatus.Unauthorized    => Result<RefreshResponse>.Unauthorized(msg),
                ResultStatus.NotFound        => Result<RefreshResponse>.NotFound(msg),
                ResultStatus.Conflict        => Result<RefreshResponse>.Conflict(msg),
                ResultStatus.InternalServerError => Result<RefreshResponse>.InternalServerError(msg),
                _ => Result<RefreshResponse>.BadRequest(msg)
            };
        }

        using var doc = JsonDocument.Parse(res.Data.Details.RawResponse);
        var accessToken  = doc.RootElement.GetProperty("access_token").GetString();
        var expiresIn    = doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 0;
        var refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        var idToken      = doc.RootElement.TryGetProperty("id_token", out var idt) ? idt.GetString() : null;

        if (string.IsNullOrEmpty(accessToken))
        {
            return Result<RefreshResponse>.InternalServerError("no_access_token_from_auth0");
        }

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(accessToken);
        var auth0id = token.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        
        string trimmedAuth0UserId = auth0id.StartsWith("auth0|")
            ? auth0id.Substring("auth0|".Length)
            : auth0id;

        var user = authRepository.GetUserByAuth0Id(trimmedAuth0UserId);
        
        var dto = new RefreshResponse(user.Id, accessToken!, expiresIn, refreshToken, idToken);
        return Result<RefreshResponse>.Success(dto, "Refresh successful");
    }

    public async Task<Result<AuthZeroBodyResponse>> LogoutAsync(string req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var res = await apiClient.RevokeRefreshTokenAsync(
            refreshToken: req,
            clientId: _config.ClientId,
            clientSecret: _config.ClientSecret,
            ct);

        if (!res.IsSuccess)
        {
            var msg = "Logout failed due to: "+ res.Data.Details.ErrorDescription;
            return res.Status switch
            {
                ResultStatus.BadRequest      => Result<AuthZeroBodyResponse>.BadRequest(msg),
                ResultStatus.Unauthorized    => Result<AuthZeroBodyResponse>.Unauthorized(msg),
                ResultStatus.NotFound        => Result<AuthZeroBodyResponse>.NotFound(msg),
                ResultStatus.Conflict        => Result<AuthZeroBodyResponse>.Conflict(msg),
                ResultStatus.InternalServerError => Result<AuthZeroBodyResponse>.InternalServerError(msg),
                _ => Result<AuthZeroBodyResponse>.BadRequest(msg)
            };
        }
        res.Data.Message = "Logout successful";
        return res;
    }
}
