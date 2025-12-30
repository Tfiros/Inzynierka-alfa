using System.Net;
using System.Net.Http.Headers;
using ItemTradeApp.AuthZeroCommunication.Dto.Response;
using ItemTradeApp.Users.AuthZeroCommunication.Mappers;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using ItemTradeApp.ApiResultHandling;

namespace ItemTradeApp.Users.AuthZeroCommunication;

public interface IAuthZeroManagementClient
{
    Task<Result<AuthZeroBodyResponse>> PatchUserAsync(
        string auth0UserId,
        object payload,
        CancellationToken ct = default);
    Task<Result<AuthZeroBodyResponse>> DeleteUserAsync(
        string auth0UserId,
        CancellationToken ct = default);
    Task<Result<List<Auth0UserSlim>>> GetAllUsersAsync(CancellationToken ct = default);
}
public sealed class AuthZeroAPIManagement : IAuthZeroManagementClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AuthZeroOptions _options;

    public AuthZeroAPIManagement(IHttpClientFactory httpFactory, IOptions<AuthZeroOptions> options)
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

        var mappedDetails = AuthZeroDetailsMapper.Build("Auth0", responseContent);

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
    public async Task<Result<AuthZeroBodyResponse>> DeleteUserAsync(
        string auth0UserId,
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
            HttpMethod.Delete,
            $"{baseUrl}/users/{encodedUserId}");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await http.SendAsync(request, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        var mappedDetails = AuthZeroDetailsMapper.Build("Auth0", responseContent);

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
                    $"auth0_mgmt_delete_error_{(int)response.StatusCode}: {responseContent}")
            };
        }

        return Result<AuthZeroBodyResponse>.NoContent("auth0_user_deleted");
    }
       public async Task<Result<List<Auth0UserSlim>>> GetAllUsersAsync(CancellationToken ct = default)
    {
        const int perPage = 100;
        var page = 0;

        var tokenRes = await GetManagementTokenAsync(ct);
        if (!tokenRes.IsSuccess || string.IsNullOrWhiteSpace(tokenRes.Data))
        {
            return new Result<List<Auth0UserSlim>>(
                isSuccess: false,
                status: tokenRes.Status,
                data: null,
                message: tokenRes.Message ?? "Failed to get Auth0 management token."
            );
        }
        var httpClient = _httpFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenRes.Data);

        var all = new List<Auth0UserSlim>();

        while (true)
        {
            var url = $"https://{_options.Domain}/api/v2/users?per_page={perPage}&page={page}&include_totals=false&fields=user_id,email,nickname,name,created_at,app_metadata&include_fields=true";
            using var resp = await httpClient.GetAsync(url, ct);

            
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);

                var status = resp.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ResultStatus.Unauthorized,
                    HttpStatusCode.BadRequest => ResultStatus.BadRequest,
                    HttpStatusCode.NotFound => ResultStatus.NotFound,
                    HttpStatusCode.Conflict => ResultStatus.Conflict,
                    _ => ResultStatus.InternalServerError
                };

                return new Result<List<Auth0UserSlim>>(
                    isSuccess: false,
                    status: status,
                    data: null,
                    message: $"Auth0 users error: {body}"
                );
            }

            var users = await resp.Content.ReadFromJsonAsync<List<AuthZeroUserResponse>>(cancellationToken: ct)
                        ?? new List<AuthZeroUserResponse>();

            foreach (var u in users)
            {
                all.Add(new Auth0UserSlim
                {
                    UserId = u.UserId,
                    Email = u.Email,
                    Nickname = u.Nickname,
                    Name = u.Name,
                    CreatedAt = u.CreatedAt,
                    Roles = u.AppMetadata?.Roles?
                        .Where(r => !string.IsNullOrWhiteSpace(r))
                        .ToList()
                        ?? new List<string>()
                });
            }

            if (users.Count < perPage)
                break;

            page++;
        }

        return new Result<List<Auth0UserSlim>>(
            isSuccess: true,
            status: ResultStatus.Success,
            data: all,
            message: null
        );
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

        var payload = await response.Content.ReadFromJsonAsync<AuthZeroTokenResponse>(cancellationToken: ct);
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            return Result<string>.Unauthorized("auth0_mgmt_token_empty");
        }

        return Result<string>.Success(payload.AccessToken);
    }
}