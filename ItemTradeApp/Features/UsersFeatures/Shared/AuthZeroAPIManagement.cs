using System.Net.Http.Headers;
using ItemTradeApp.AuthZeroCommunication.Dto.ResponseDtos;
using ItemTradeApp.AuthZeroCommunication.Mappers;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace ItemTradeApp.AuthZeroCommunication;

public interface IAuthZeroManagementClient
{
    Task<Result<AuthZeroBodyResponse>> PatchUserAsync(
        string auth0UserId,
        object payload,
        CancellationToken ct = default);
}
public sealed class AuthZeroAPIManagement : IAuthZeroManagementClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly Auth0Options _options;

    public AuthZeroAPIManagement(IHttpClientFactory httpFactory, IOptions<Auth0Options> options)
    {
        _httpFactory = httpFactory;
        _options = options.Value;
    }

    public async Task<Result<AuthZeroBodyResponse>> PatchUserAsync(
        string auth0UserId,
        object payload,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
        {
            return Result<AuthZeroBodyResponse>.BadRequest("auth0_user_id_required");
        }

        var tokenResult = await GetManagementTokenAsync(ct);
        if (!tokenResult.IsSuccess || string.IsNullOrWhiteSpace(tokenResult.Data))
        {
            return Result<AuthZeroBodyResponse>.Unauthorized(
                tokenResult.Message ?? "auth0_mgmt_token_error");
        }

        var token   = tokenResult.Data;
        var baseUrl = $"https://{_options.Domain}/api/v2";
        var encodedUserId = Uri.EscapeDataString(auth0UserId);

        var http = _httpFactory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"{baseUrl}/users/{encodedUserId}")
        {
            Content = JsonContent.Create(payload)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await http.SendAsync(request, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        var mappedDetails = Auth0DetailsMapper.Build("Auth0", responseContent);

        if (!response.IsSuccessStatusCode)
        {
            return response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized =>
                    Result<AuthZeroBodyResponse>.Unauthorized("auth0_mgmt_unauthorized"),

                System.Net.HttpStatusCode.Forbidden =>
                    Result<AuthZeroBodyResponse>.Unauthorized("auth0_mgmt_forbidden"),

                System.Net.HttpStatusCode.NotFound =>
                    Result<AuthZeroBodyResponse>.NotFound("auth0_user_not_found"),

                _ => Result<AuthZeroBodyResponse>.BadRequest(
                    $"auth0_mgmt_error_{(int)response.StatusCode}: {responseContent}")
            };
        }
        
        return Result<AuthZeroBodyResponse>.Success(mappedDetails, "auth0_user_updated");
    }

    private async Task<Result<string>> GetManagementTokenAsync(CancellationToken ct)
    {
        var mgmt = _options.Management;

        var audience = string.IsNullOrWhiteSpace(mgmt.Audience)
            ? $"https://{_options.Domain}/api/v2/"
            : mgmt.Audience;

        var body = new
        {
            client_id     = mgmt.ClientId,
            client_secret = mgmt.ClientSecret,
            audience,
            grant_type    = "client_credentials"
        };

        var tokenUrl = $"https://{_options.Domain}/oauth/token";

        var http = _httpFactory.CreateClient();

        using var response = await http.PostAsJsonAsync(tokenUrl, body, ct);


        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return Result<string>.Unauthorized($"auth0_mgmt_token_failed: {error}");
        }

        var payload = await response.Content.ReadFromJsonAsync<Auth0TokenResponse>(cancellationToken: ct);
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            return Result<string>.Unauthorized("auth0_mgmt_token_empty");
        }

        return Result<string>.Success(payload.AccessToken);
    }
}