using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration.DTOs.Response;
using ItemTradeApp.Users.AuthZeroCommunication.Mappers;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.Features.Users.Shared.AuthZeroIntegration;

public interface IAuthZeroAPIClient
{
    Task<Result<AuthZeroBodyResponse>> SignupAsync(
        string email,
        string password,
        string connection,
        string clientId,
        string? name,
        CancellationToken ct = default);

    Task<Result<AuthZeroBodyResponse>> PasswordRealmTokenAsync(
        string username,
        string password,
        string realm,
        string clientId,
        string clientSecret,
        string audience,
        string scope,
        CancellationToken ct = default);

    Task<Result<AuthZeroBodyResponse>> ChangePasswordAsync(
        string email,
        string connection,
        string clientId,
        CancellationToken ct = default);

    Task<Result<AuthZeroBodyResponse>> RefreshTokenAsync(
        string refreshToken,
        string clientId,
        string clientSecret,
        string? scope,
        CancellationToken ct = default);

    Task<Result<AuthZeroBodyResponse>> RevokeRefreshTokenAsync(
        string refreshToken,
        string clientId,
        string clientSecret,
        CancellationToken ct = default);
}

public class AuthZeroAPIClient : IAuthZeroAPIClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly JsonSerializerOptions _jsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    public string BaseUrl { get; }
    private readonly ILogger<AuthZeroAPIClient> _logger;

    public AuthZeroAPIClient(
        IHttpClientFactory httpFactory,
        IOptions<AuthZeroOptions> opts,
        ILogger<AuthZeroAPIClient> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;

        var domain = opts.Value.Domain?.Trim().TrimEnd('/')
                     ?? throw new InvalidOperationException("Auth0:Domain is missing");

        BaseUrl = domain.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? domain
            : $"https://{domain}";
    }

    public Task<Result<AuthZeroBodyResponse>> SignupAsync(
        string email,
        string password,
        string connection,
        string clientId,
        string? name,
        CancellationToken ct = default)
    {
        var payload = new
        {
            client_id = clientId,
            email,
            password,
            connection,
            name
        };
        return PostJsonAsync("/dbconnections/signup", payload, "auth0_signup", ct);
    }

    public Task<Result<AuthZeroBodyResponse>> PasswordRealmTokenAsync(
        string username,
        string password,
        string realm,
        string clientId,
        string clientSecret,
        string audience,
        string scope,
        CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"]    = "http://auth0.com/oauth/grant-type/password-realm",
            ["realm"]         = realm,
            ["username"]      = username,
            ["password"]      = password,
            ["client_id"]     = clientId,
            ["client_secret"] = clientSecret,
            ["audience"]      = audience,
            ["scope"]         = scope
        };
        return PostFormAsync("/oauth/token", form, "auth0_password_realm", ct);
    }

    public Task<Result<AuthZeroBodyResponse>> ChangePasswordAsync(
        string email,
        string connection,
        string clientId,
        CancellationToken ct = default)
    {
        var payload = new
        {
            client_id = clientId,
            email,
            connection
        };
        return PostJsonAsync("/dbconnections/change_password", payload, "auth0_change_password", ct);
    }

    public Task<Result<AuthZeroBodyResponse>> RefreshTokenAsync(
        string refreshToken,
        string clientId,
        string clientSecret,
        string? scope,
        CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"]    = "refresh_token",
            ["client_id"]     = clientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = refreshToken
        };
        if (!string.IsNullOrWhiteSpace(scope))
            form["scope"] = scope!;
        return PostFormAsync("/oauth/token", form, "auth0_refresh_token", ct);
    }

    public Task<Result<AuthZeroBodyResponse>> RevokeRefreshTokenAsync(
        string refreshToken,
        string clientId,
        string clientSecret,
        CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"]     = clientId,
            ["client_secret"] = clientSecret,
            ["token"]         = refreshToken
        };
        return PostFormAsync("/oauth/revoke", form, "auth0_revoke_token", ct);
    }
    
    private async Task<Result<AuthZeroBodyResponse>> PostJsonAsync(
        string path,
        object payload,
        string operationName,
        CancellationToken ct)
    {
        try
        {
            var httpClient = _httpFactory.CreateClient("Auth0Public");
            using var req = new HttpRequestMessage(HttpMethod.Post, Combine(BaseUrl, path));
            var json = JsonSerializer.Serialize(payload, _jsonOpts);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var resp = await httpClient.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (resp.IsSuccessStatusCode)
            {
                var data = AuthZeroDetailsMapper.Build($"{operationName}_success", body);
                return Result<AuthZeroBodyResponse>.Success(data, $"{operationName}_success");
            }

            return MapHttpErrorToResult(body, resp.StatusCode, operationName, resp.ReasonPhrase);
        }
        catch (HttpRequestException ex)
        {
            var data = AuthZeroDetailsMapper.Build($"{operationName}_http_failed", ex.Message);
            return new Result<AuthZeroBodyResponse>(
                false,
                ResultStatus.InternalServerError,
                data,
                "http_request_failed");
        }
    }
    private async Task<Result<AuthZeroBodyResponse>> PostFormAsync(
        string path,
        Dictionary<string, string> form,
        string operationName,
        CancellationToken ct)
    {
        try
        {
            var httpClient = _httpFactory.CreateClient("Auth0Public");
            using var req = new HttpRequestMessage(HttpMethod.Post, Combine(BaseUrl, path))
            {
                Content = new FormUrlEncodedContent(form)
            };
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var resp = await httpClient.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (resp.IsSuccessStatusCode)
            {
                var data = AuthZeroDetailsMapper.Build($"{operationName}_success", body);
                return Result<AuthZeroBodyResponse>.Success(data, $"{operationName}_success");
            }

            return MapHttpErrorToResult(body, resp.StatusCode, operationName, resp.ReasonPhrase);
        }
        catch (HttpRequestException ex)
        {
            var data = AuthZeroDetailsMapper.Build($"{operationName}_http_failed", ex.Message);
            return new Result<AuthZeroBodyResponse>(
                false,
                ResultStatus.InternalServerError,
                data,
                "http_request_failed");
        }
    }

    private Result<AuthZeroBodyResponse> MapHttpErrorToResult(
        string body,
        HttpStatusCode statusCode,
        string operationName,
        string? reason)
    {
        _logger.LogWarning(
            "Auth0 operation failed. Operation: {Operation}, Status: {StatusCode}, Reason: {Reason}, Body: {Body}",
            operationName,
            statusCode,
            reason,
            body);

        var data = AuthZeroDetailsMapper.Build($"{operationName}_failed", body);

        var status = statusCode switch
        {
            HttpStatusCode.BadRequest => ResultStatus.BadRequest,
            HttpStatusCode.Unauthorized => ResultStatus.Unauthorized,
            HttpStatusCode.NotFound => ResultStatus.NotFound,
            HttpStatusCode.Conflict => ResultStatus.Conflict,
            _ when (int)statusCode is >= 500 and < 600 => ResultStatus.InternalServerError,
            _ => ResultStatus.BadRequest
        };

        return new Result<AuthZeroBodyResponse>(
            false,
            status,
            data,
            $"{operationName}_failed");
    }

    private static string Combine(string baseUrl, string path)
        => $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
}

