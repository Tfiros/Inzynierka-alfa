using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration.DTOs.Response;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.Features.Users.Shared.AuthZeroIntegration;

public interface IAuthZeroManagementTokenProvider
{
    Task<Result<string>> GetTokenAsync(CancellationToken ct = default);
}

public sealed class AuthZeroManagementTokenProvider(
    IHttpClientFactory httpFactory,
    IOptions<AuthZeroOptions> options,
    ILogger<AuthZeroManagementTokenProvider> logger) : IAuthZeroManagementTokenProvider
{
    private readonly AuthZeroOptions _options = options.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _cachedTokenExpiresAt;

    public async Task<Result<string>> GetTokenAsync(CancellationToken ct = default)
    {
        if (IsTokenValid())
            return Result<string>.Success(_cachedToken!);

        await _lock.WaitAsync(ct);

        try
        {
            if (IsTokenValid())
                return Result<string>.Success(_cachedToken!);

            var mgmt = _options.Management;

            var audience = string.IsNullOrWhiteSpace(mgmt.Audience)
                ? $"https://{_options.Domain}/api/v2/"
                : mgmt.Audience;

            var payload = new
            {
                client_id = mgmt.ClientId,
                client_secret = mgmt.ClientSecret,
                audience,
                grant_type = "client_credentials"
            };

            var http = httpFactory.CreateClient("Auth0Management");
            var tokenUrl = $"https://{_options.Domain}/oauth/token";

            using var response = await http.PostAsJsonAsync(tokenUrl, payload, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Auth0 management token request failed. Status: {StatusCode}, Body: {Body}",
                    response.StatusCode,
                    body);

                return Result<string>.Unauthorized("auth_provider_token_error");
            }

            var token = await response.Content.ReadFromJsonAsync<AuthZeroTokenResponse>(cancellationToken: ct);

            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                logger.LogWarning("Auth0 management token response was empty. Body: {Body}", body);
                return Result<string>.Unauthorized("auth_provider_token_empty");
            }

            var expiresIn = token.ExpiresIn <= 0 ? 3600 : token.ExpiresIn;

            _cachedToken = token.AccessToken;
            _cachedTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

            return Result<string>.Success(token.AccessToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auth0 management token request failed unexpectedly.");
            return Result<string>.InternalServerError("auth_provider_token_error");
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsTokenValid()
    {
        return !string.IsNullOrWhiteSpace(_cachedToken)
               && _cachedTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1);
    }
}