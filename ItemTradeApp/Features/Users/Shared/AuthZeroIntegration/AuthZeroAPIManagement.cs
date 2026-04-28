using System.Net;
using System.Net.Http.Headers;
using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration.DTOs.Response;
using ItemTradeApp.Users.AuthZeroCommunication;
using ItemTradeApp.Users.AuthZeroCommunication.Mappers;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.Features.Users.Shared.AuthZeroIntegration;

public interface IAuthZeroManagementClient
{
    Task<Result<AuthZeroBodyResponse>> PatchUserAsync(string auth0UserId, object payload, CancellationToken ct = default);
    Task<Result<AuthZeroBodyResponse>> DeleteUserAsync(string auth0UserId, CancellationToken ct = default);
    Task<Result<List<Auth0UserSlim>>> GetUsersInRoleAsync(string roleId, CancellationToken ct = default);

    Task<Result<string>> AssignRolesToUserAsync(string auth0UserId, IReadOnlyCollection<string> roleIds, CancellationToken ct = default);
    Task<Result<string>> RemoveRolesFromUserAsync(string auth0UserId, IReadOnlyCollection<string> roleIds, CancellationToken ct = default);

    Task<Result<List<Auth0RoleResponse>>> GetRolesAsync(CancellationToken ct = default);
    Task<Result<List<Auth0RoleResponse>>> GetUserRolesAsync(string auth0UserId, CancellationToken ct = default);
}

public sealed class AuthZeroAPIManagement : IAuthZeroManagementClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AuthZeroOptions _options;
    private readonly IAuth0ManagementTokenProvider _tokenProvider;
    private readonly ILogger<AuthZeroAPIManagement> _logger;

    public AuthZeroAPIManagement(
        IHttpClientFactory httpFactory,
        IOptions<AuthZeroOptions> options,
        IAuth0ManagementTokenProvider tokenProvider,
        ILogger<AuthZeroAPIManagement> logger)
    {
        _httpFactory = httpFactory;
        _options = options.Value;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public async Task<Result<AuthZeroBodyResponse>> PatchUserAsync(string auth0UserId, object payload, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<AuthZeroBodyResponse>.BadRequest("auth0_user_id_required");

        var tokenRes = await _tokenProvider.GetTokenAsync(ct);
        if (!tokenRes.IsSuccess || string.IsNullOrWhiteSpace(tokenRes.Data))
            return Result<AuthZeroBodyResponse>.Unauthorized(tokenRes.Message ?? "auth0_mgmt_token_error");

        var baseUrl = $"https://{_options.Domain}/api/v2";
        var encodedUserId = Uri.EscapeDataString(auth0UserId);

        var httpClient = _httpFactory.CreateClient("Auth0Management");
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{baseUrl}/users/{encodedUserId}")
        {
            Content = JsonContent.Create(payload)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenRes.Data);

        using var response = await httpClient.SendAsync(request, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        var mappedDetails = AuthZeroDetailsMapper.Build("Auth0", responseContent);

        if (!response.IsSuccessStatusCode)
        {
            return MapAuth0ManagementError<AuthZeroBodyResponse>(
                response.StatusCode,
                responseContent,
                "patch_user",
                auth0UserId);
        }

        return Result<AuthZeroBodyResponse>.Success(mappedDetails, "auth0_user_updated");
    }
   public async Task<Result<List<Auth0UserSlim>>> GetUsersInRoleAsync(string roleId, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(roleId))
        return Result<List<Auth0UserSlim>>.BadRequest("auth0_role_id_required");

    const int perPage = 100;
    var page = 0;

    var tokenRes = await _tokenProvider.GetTokenAsync(ct);
    if (!tokenRes.IsSuccess || string.IsNullOrWhiteSpace(tokenRes.Data))
        return new Result<List<Auth0UserSlim>>(false, tokenRes.Status, null, tokenRes.Message ?? "auth0_mgmt_token_error");

    var httpClient = _httpFactory.CreateClient("Auth0Management");
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenRes.Data);

    var all = new List<Auth0UserSlim>();
    var encodedRoleId = Uri.EscapeDataString(roleId);

    while (true)
    {
        var url = $"https://{_options.Domain}/api/v2/roles/{encodedRoleId}/users?per_page={perPage}&page={page}";
        using var resp = await httpClient.GetAsync(url, ct);

        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            var status = resp.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ResultStatus.Unauthorized,
                HttpStatusCode.BadRequest => ResultStatus.BadRequest,
                HttpStatusCode.NotFound => ResultStatus.NotFound,
                HttpStatusCode.Conflict => ResultStatus.Conflict,
                _ => ResultStatus.InternalServerError
            };

            return new Result<List<Auth0UserSlim>>(false, status, null,
                $"auth0_get_users_in_role_error_{(int)resp.StatusCode}: {body}");
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
                Roles = new List<string>()
            });
        }

        if (users.Count < perPage)
            break;

        page++;
    }

    return new Result<List<Auth0UserSlim>>(true, ResultStatus.Success, all, null);
}

    public async Task<Result<AuthZeroBodyResponse>> DeleteUserAsync(string auth0UserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<AuthZeroBodyResponse>.BadRequest("auth0_user_id_required");

        var tokenRes = await _tokenProvider.GetTokenAsync(ct);
        if (!tokenRes.IsSuccess || string.IsNullOrWhiteSpace(tokenRes.Data))
            return Result<AuthZeroBodyResponse>.Unauthorized(tokenRes.Message ?? "auth0_mgmt_token_error");

        var baseUrl = $"https://{_options.Domain}/api/v2";
        var encodedUserId = Uri.EscapeDataString(auth0UserId);

        var httpClient = _httpFactory.CreateClient("Auth0Management");
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{baseUrl}/users/{encodedUserId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenRes.Data);

        using var response = await httpClient.SendAsync(request, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        var mappedDetails = AuthZeroDetailsMapper.Build("Auth0", responseContent);

        if (!response.IsSuccessStatusCode)
        {
            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => Result<AuthZeroBodyResponse>.Unauthorized("auth0_mgmt_unauthorized"),
                HttpStatusCode.Forbidden => Result<AuthZeroBodyResponse>.Unauthorized("auth0_mgmt_forbidden"),
                HttpStatusCode.NotFound => Result<AuthZeroBodyResponse>.NotFound("auth0_user_not_found"),
                _ => Result<AuthZeroBodyResponse>.BadRequest($"auth0_mgmt_delete_error_{(int)response.StatusCode}: {responseContent}")
            };
        }

        return Result<AuthZeroBodyResponse>.NoContent("auth0_user_deleted");
    }

    public async Task<Result<List<Auth0UserSlim>>> GetAllUsersAsync(CancellationToken ct = default)
    {
        const int perPage = 100;
        var page = 0;

        var tokenRes = await _tokenProvider.GetTokenAsync(ct);
        if (!tokenRes.IsSuccess || string.IsNullOrWhiteSpace(tokenRes.Data))
        {
            return new Result<List<Auth0UserSlim>>(
                isSuccess: false,
                status: tokenRes.Status,
                data: null,
                message: tokenRes.Message ?? "Failed to get Auth0 management token.");
        }

        var httpClient = _httpFactory.CreateClient("Auth0Management");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenRes.Data);

        var all = new List<Auth0UserSlim>();

        while (true)
        {
            var url = $"https://{_options.Domain}/api/v2/users?per_page={perPage}&page={page}&include_totals=false&fields=user_id,email,nickname,name,created_at&include_fields=true";
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
                    message: $"Auth0 users error: {body}");
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
                    Roles = new List<string>()
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
            message: null);
    }

    public async Task<Result<List<Auth0RoleResponse>>> GetRolesAsync(CancellationToken ct = default)
    {
        var tokenRes = await _tokenProvider.GetTokenAsync(ct);
        if (!tokenRes.IsSuccess || string.IsNullOrWhiteSpace(tokenRes.Data))
            return Result<List<Auth0RoleResponse>>.Unauthorized(tokenRes.Message ?? "auth0_mgmt_token_error");

        var httpClient = _httpFactory.CreateClient("Auth0Management");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenRes.Data);

        var url = $"https://{_options.Domain}/api/v2/roles?per_page=100&page=0";
        using var resp = await httpClient.GetAsync(url, ct);

        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            return MapAuth0ManagementError<List<Auth0RoleResponse>>(
                resp.StatusCode,
                body,
                "get_roles");
        }

        var roles = await resp.Content.ReadFromJsonAsync<List<Auth0RoleResponse>>(cancellationToken: ct)
                    ?? new List<Auth0RoleResponse>();

        return Result<List<Auth0RoleResponse>>.Success(roles);
    }

    public async Task<Result<List<Auth0RoleResponse>>> GetUserRolesAsync(string auth0UserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<List<Auth0RoleResponse>>.BadRequest("auth0_user_id_required");

        var tokenRes = await _tokenProvider.GetTokenAsync(ct);
        if (!tokenRes.IsSuccess || string.IsNullOrWhiteSpace(tokenRes.Data))
            return Result<List<Auth0RoleResponse>>.Unauthorized(tokenRes.Message ?? "auth0_mgmt_token_error");

        var httpClient = _httpFactory.CreateClient("Auth0Management");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenRes.Data);

        var encodedUserId = Uri.EscapeDataString(auth0UserId);
        var url = $"https://{_options.Domain}/api/v2/users/{encodedUserId}/roles?per_page=100&page=0";

        using var resp = await httpClient.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            return Result<List<Auth0RoleResponse>>.BadRequest($"auth0_get_user_roles_error_{(int)resp.StatusCode}: {body}");

        var roles = await resp.Content.ReadFromJsonAsync<List<Auth0RoleResponse>>(cancellationToken: ct)
                    ?? new List<Auth0RoleResponse>();

        return Result<List<Auth0RoleResponse>>.Success(roles);
    }

    public async Task<Result<string>> AssignRolesToUserAsync(string auth0UserId, IReadOnlyCollection<string> roleIds, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<string>.BadRequest("auth0_user_id_required");

        if (roleIds is null || roleIds.Count == 0)
            return Result<string>.BadRequest("role_ids_required");

        var tokenRes = await _tokenProvider.GetTokenAsync(ct);
        if (!tokenRes.IsSuccess || string.IsNullOrWhiteSpace(tokenRes.Data))
            return Result<string>.Unauthorized(tokenRes.Message ?? "auth0_mgmt_token_error");

        var httpClient = _httpFactory.CreateClient("Auth0Management");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenRes.Data);

        var encodedUserId = Uri.EscapeDataString(auth0UserId);
        var url = $"https://{_options.Domain}/api/v2/users/{encodedUserId}/roles";

        using var resp = await httpClient.PostAsJsonAsync(url, new { roles = roleIds }, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            return Result<string>.BadRequest($"auth0_assign_roles_error_{(int)resp.StatusCode}: {body}");

        return Result<string>.NoContent("auth0_roles_assigned");
    }

    public async Task<Result<string>> RemoveRolesFromUserAsync(string auth0UserId, IReadOnlyCollection<string> roleIds, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<string>.BadRequest("auth0_user_id_required");

        if (roleIds is null || roleIds.Count == 0)
            return Result<string>.BadRequest("role_ids_required");

        var tokenRes = await _tokenProvider.GetTokenAsync(ct);
        if (!tokenRes.IsSuccess || string.IsNullOrWhiteSpace(tokenRes.Data))
            return Result<string>.Unauthorized(tokenRes.Message ?? "auth0_mgmt_token_error");

        var httpClient = _httpFactory.CreateClient("Auth0Management");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenRes.Data);

        var encodedUserId = Uri.EscapeDataString(auth0UserId);
        var url = $"https://{_options.Domain}/api/v2/users/{encodedUserId}/roles";

        using var req = new HttpRequestMessage(HttpMethod.Delete, url)
        {
            Content = JsonContent.Create(new { roles = roleIds })
        };

        using var resp = await httpClient.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            return Result<string>.BadRequest($"auth0_remove_roles_error_{(int)resp.StatusCode}: {body}");

        return Result<string>.NoContent("auth0_roles_removed");
    }
    private Result<T> MapAuth0ManagementError<T>(
        HttpStatusCode statusCode,
        string responseContent,
        string operationName,
        string? auth0UserId = null)
    {
        _logger.LogWarning(
            "Auth0 management operation failed. Operation: {Operation}, UserId: {Auth0UserId}, Status: {StatusCode}, Body: {Body}",
            operationName,
            auth0UserId,
            statusCode,
            responseContent);

        return statusCode switch
        {
            HttpStatusCode.Unauthorized =>
                Result<T>.Unauthorized("auth_provider_unauthorized"),

            HttpStatusCode.Forbidden =>
                Result<T>.Unauthorized("auth_provider_forbidden"),

            HttpStatusCode.NotFound =>
                Result<T>.NotFound("auth_resource_not_found"),

            HttpStatusCode.Conflict =>
                Result<T>.Conflict("auth_conflict"),

            HttpStatusCode.BadRequest =>
                Result<T>.BadRequest("auth_provider_bad_request"),

            _ when (int)statusCode is >= 500 and < 600 =>
                Result<T>.InternalServerError("auth_provider_unavailable"),

            _ =>
                Result<T>.BadRequest("auth_provider_error")
        };
    }

}
