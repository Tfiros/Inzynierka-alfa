// src/ItemTradeApp/LoginFeature/AuthZeroAPIClient.cs
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ItemTradeApp.ExceptionsHandling;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.LoginFeature;
public interface IAuthZeroAPIClient
{
    Task<Result<string>> SignupAsync(string email, string password, string connection, string clientId, string? name, CancellationToken ct = default);
    Task<Result<string>> PasswordRealmTokenAsync(string username, string password, string realm, string clientId, string clientSecret, string audience, string scope, CancellationToken ct = default);
    Task<Result<string>> ChangePasswordAsync(string email, string connection, string clientId, CancellationToken ct = default);
    Task<Result<string>> RefreshTokenAsync(string refreshToken, string clientId, string clientSecret, string? scope, CancellationToken ct = default);
    Task<Result<string>> RevokeRefreshTokenAsync(string refreshToken, string clientId, string clientSecret, CancellationToken ct = default);
}
public class AuthZeroAPIClient : IAuthZeroAPIClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly JsonSerializerOptions _jsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public string BaseUrl { get; }

    public AuthZeroAPIClient(IHttpClientFactory httpFactory, IOptions<Auth0Options> opts)
    {
        _httpFactory = httpFactory;
        var domain = opts.Value.Domain?.Trim().TrimEnd('/')
                     ?? throw new InvalidOperationException("Auth0:Domain is missing");
        BaseUrl = domain.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? domain : $"https://{domain}";
    }

    public Task<Result<string>> SignupAsync(
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
        return PostJsonAsync("/dbconnections/signup", payload, ct);
    }

    public Task<Result<string>> PasswordRealmTokenAsync(
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
        return PostFormAsync("/oauth/token", form, ct);
    }

    public Task<Result<string>> ChangePasswordAsync(
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
        return PostJsonAsync("/dbconnections/change_password", payload, ct);
    }

    public Task<Result<string>> RefreshTokenAsync(
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
        return PostFormAsync("/oauth/token", form, ct);
    }

    public Task<Result<string>> RevokeRefreshTokenAsync(
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
        return PostFormAsync("/oauth/revoke", form, ct);
    }

    private async Task<Result<string>> PostJsonAsync(string path, object payload, CancellationToken ct)
    {
        try
        {
            var http = _httpFactory.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, Combine(BaseUrl, path));
            var json = JsonSerializer.Serialize(payload, _jsonOpts);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var resp = await http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return resp.IsSuccessStatusCode
                ? Result<string>.Ok(body)
                : Result<string>.Fail(new AppError((int)resp.StatusCode, body, resp.ReasonPhrase));
        }
        catch (HttpRequestException ex)
        {
            return Result<string>.Fail(new AppError(502, ex.Message, "http_request_failed"));
        }
    }

    private async Task<Result<string>> PostFormAsync(string path, Dictionary<string, string> form, CancellationToken ct)
    {
        try
        {
            var http = _httpFactory.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, Combine(BaseUrl, path))
            {
                Content = new FormUrlEncodedContent(form)
            };
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var resp = await http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return resp.IsSuccessStatusCode
                ? Result<string>.Ok(body)
                : Result<string>.Fail(new AppError((int)resp.StatusCode, body, resp.ReasonPhrase));
        }
        catch (HttpRequestException ex)
        {
            return Result<string>.Fail(new AppError(502, ex.Message, "http_request_failed"));
        }
    }

    private static string Combine(string baseUrl, string path)
        => $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
}
