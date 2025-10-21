using System.Text.Json;
using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.LoginFeature.Dto;
using ItemTradeApp.LoginFeature.Dto.ResponseDtos;
using ItemTradeApp.LoginFeature.Mappers;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.LoginFeature;

public interface ILoginService
{
    Task<Result<RawBodyResponseDto>> RegisterAsync(RegisterRequest req, CancellationToken ct = default);
    Task<Result<LoginResponseDto>>   LoginAsync(LoginRequest req, CancellationToken ct = default);
    Task<Result<RawBodyResponseDto>> ForgotPasswordAsync(ForgotPasswordRequest req, CancellationToken ct = default);
    Task<Result<RefreshResponseDto>> RefreshAsync(RefreshTokenRequest req, CancellationToken ct = default);
}
public class AuthService(IOptions<Auth0Options> config, ILoginAPIClient apiClient) : ILoginService
{
    private readonly Auth0Options _config = config.Value;
    
    public async Task<Result<RawBodyResponseDto>> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var payload = new
        {
            client_id  = _config.ClientId,
            email      = req.Email,
            password   = req.Password,
            connection = _config.Realm,
            user_metadata = req.Metadata,
            name       = req.Name
        };

        var res = await apiClient.PostJsonAsync("/dbconnections/signup", payload, ct);

        if (res.IsSuccess)
            return Result<RawBodyResponseDto>.Ok(Auth0DetailsMapper.Build("registration_success", res.Value!));

        return Result<RawBodyResponseDto>.Fail(res.Error!.Value);
    }

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var form = new Dictionary<string, string>
        {
            ["grant_type"]    = "http://auth0.com/oauth/grant-type/password-realm",
            ["realm"]         = _config.Realm,
            ["username"]      = req.Username,
            ["password"]      = req.Password,
            ["client_id"]     = _config.ClientId,
            ["client_secret"] = _config.ClientSecret,
            ["audience"]      = _config.Audience,
            ["scope"]         = "openid profile email offline_access"
        };

        var res = await apiClient.PostFormAsync("/oauth/token", form, ct);
        if (!res.IsSuccess) return Result<LoginResponseDto>.Fail(res.Error!.Value);

        using var doc = JsonDocument.Parse(res.Value!);
        var accessToken  = doc.RootElement.GetProperty("access_token").GetString();
        var expiresIn    = doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 0;
        var refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        var idToken      = doc.RootElement.TryGetProperty("id_token", out var idt) ? idt.GetString() : null;

        if (string.IsNullOrEmpty(accessToken))
            return Result<LoginResponseDto>.Fail(new AppError(502, res.Value!, "no_access_token_from_auth0"));

        return Result<LoginResponseDto>.Ok(new LoginResponseDto
        (
            accessToken!,
          expiresIn,
            refreshToken,
            idToken
        ));
    }

    public async Task<Result<RawBodyResponseDto>> ForgotPasswordAsync(ForgotPasswordRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var payload = new
        {
            client_id  = _config.ClientId,
            email      = req.Email,
            connection = _config.Realm
        };

        var res = await apiClient.PostJsonAsync("/dbconnections/change_password", payload, ct);

        if (res.IsSuccess)
            return Result<RawBodyResponseDto>.Ok(Auth0DetailsMapper.Build("reset_email_sent", res.Value!));

        return Result<RawBodyResponseDto>.Fail(res.Error!.Value);
    }

    public async Task<Result<RefreshResponseDto>> RefreshAsync(RefreshTokenRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var form = new Dictionary<string, string>
        {
            ["grant_type"]    = "refresh_token",
            ["client_id"]     = _config.ClientId,
            ["client_secret"] = _config.ClientSecret,
            ["refresh_token"] = req.RefreshToken
        };
        if (!string.IsNullOrWhiteSpace(req.Scope))
            form["scope"] = req.Scope!;

        var res = await apiClient.PostFormAsync("/oauth/token", form, ct);
        if (!res.IsSuccess) return Result<RefreshResponseDto>.Fail(res.Error!.Value);

        using var doc = JsonDocument.Parse(res.Value!);
        var accessToken  = doc.RootElement.GetProperty("access_token").GetString();
        var expiresIn    = doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 0;
        var refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var r) ? r.GetString() : null; // przy rotacji
        var idToken      = doc.RootElement.TryGetProperty("id_token", out var idt) ? idt.GetString() : null;

        if (string.IsNullOrEmpty(accessToken))
            return Result<RefreshResponseDto>.Fail(new AppError(502, res.Value!, "no_access_token_from_auth0"));

        return Result<RefreshResponseDto>.Ok(new RefreshResponseDto
        (
           accessToken!,
           expiresIn,
            refreshToken,
            idToken
        ));
    }
}