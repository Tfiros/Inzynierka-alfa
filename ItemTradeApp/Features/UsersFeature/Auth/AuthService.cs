using System.Text.Json;
using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.UsersFeature.Auth.Dto.RequestDtos;
using ItemTradeApp.LoginFeature.Dto;
using ItemTradeApp.LoginFeature.Dto.RequestDtos;
using ItemTradeApp.LoginFeature.Dto.ResponseDtos;
using ItemTradeApp.LoginFeature.Mappers;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.LoginFeature;

public interface IAuthService
{
    Task<Result<RawBodyResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default);
    Task<Result<LoginResponse>>   LoginAsync(LoginRequest req, CancellationToken ct = default);
    Task<Result<RawBodyResponse>> ForgotPasswordAsync(ForgotPasswordRequest req, CancellationToken ct = default);
    Task<Result<RefreshResponse>> RefreshAsync(string req, CancellationToken ct = default);
    Task<Result<RawBodyResponse>> LogoutAsync(string req, CancellationToken ct = default);
}
public class AuthService(IOptions<Auth0Options> config, IAuthZeroAPIClient apiClient,
    IAuthRepository authRepository) : IAuthService
{
    private readonly Auth0Options _config = config.Value;

    public async Task<Result<RawBodyResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var res = await apiClient.SignupAsync(
            email: req.Email,
            password: req.Password,
            connection: _config.Realm,
            clientId: _config.ClientId,
            name: req.Username,
            ct);
        var doc = JsonDocument.Parse(res.Value?.ToString());
        var auth0Id = doc.RootElement.GetProperty("_id").GetString() ?? string.Empty;
        await authRepository.Register(req, auth0Id);
        return res.IsSuccess
            ? Result<RawBodyResponse>.Ok(Auth0DetailsMapper.Build("registration_success", res.Value!))
            : Result<RawBodyResponse>.Fail(res.Error!.Value);
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

        if (!res.IsSuccess) return Result<LoginResponse>.Fail(res.Error!.Value);

        using var doc = JsonDocument.Parse(res.Value!);
        var accessToken  = doc.RootElement.GetProperty("access_token").GetString();
        var expiresIn    = doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 0;
        var refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        var idToken      = doc.RootElement.TryGetProperty("id_token", out var idt) ? idt.GetString() : null;
        
        if (string.IsNullOrEmpty(accessToken))
            return Result<LoginResponse>.Fail(new AppError(502, res.Value!, "no_access_token_from_auth0"));
        var user = await authRepository.GetUserIdByEmail(req.Email);
        return Result<LoginResponse>.Ok(new LoginResponse(user.ID, accessToken!, expiresIn, refreshToken, idToken));
    }

    public async Task<Result<RawBodyResponse>> ForgotPasswordAsync(ForgotPasswordRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var res = await apiClient.ChangePasswordAsync(
            email: req.Email,
            connection: _config.Realm,
            clientId: _config.ClientId,
            ct);

        return res.IsSuccess
            ? Result<RawBodyResponse>.Ok(Auth0DetailsMapper.Build("reset_email_sent", res.Value!))
            : Result<RawBodyResponse>.Fail(res.Error!.Value);
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

        if (!res.IsSuccess) return Result<RefreshResponse>.Fail(res.Error!.Value);

        using var doc = JsonDocument.Parse(res.Value!);
        var accessToken  = doc.RootElement.GetProperty("access_token").GetString();
        var expiresIn    = doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 0;
        var refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        var idToken      = doc.RootElement.TryGetProperty("id_token", out var idt) ? idt.GetString() : null;

        if (string.IsNullOrEmpty(accessToken))
            return Result<RefreshResponse>.Fail(new AppError(502, res.Value!, "no_access_token_from_auth0"));

        return Result<RefreshResponse>.Ok(new RefreshResponse(accessToken!, expiresIn, refreshToken, idToken));
    }

    public async Task<Result<RawBodyResponse>> LogoutAsync(string req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var res = await apiClient.RevokeRefreshTokenAsync(
            refreshToken: req,
            clientId: _config.ClientId,
            clientSecret: _config.ClientSecret,
            ct);

        return res.IsSuccess
            ? Result<RawBodyResponse>.Ok(Auth0DetailsMapper.Build("auth0_revoke_success", res.Value ?? string.Empty))
            : Result<RawBodyResponse>.Fail(res.Error!.Value);
    }
}