using System.Net;
using System.Net.Http.Headers;
using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration.DTOs.Response;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration.Mappers;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.Features.Users.Shared.AuthZeroIntegration;

public interface IAuthZeroManagementClient
{
    Task<Result<AuthZeroBodyResponse>> PatchUserAsync(string auth0UserId, object payload, CancellationToken ct = default);
    Task<Result<AuthZeroBodyResponse>> DeleteUserAsync(string auth0UserId, CancellationToken ct = default);
    Task<Result<List<AuthZeroUserSlim>>> GetUsersInRoleAsync(string roleId, CancellationToken ct = default);

    Task<Result<string>> AssignRolesToUserAsync(string auth0UserId, IReadOnlyCollection<string> roleIds, CancellationToken ct = default);
    Task<Result<string>> RemoveRolesFromUserAsync(string auth0UserId, IReadOnlyCollection<string> roleIds, CancellationToken ct = default);

    Task<Result<List<AuthZeroRoleResponse>>> GetRolesAsync(CancellationToken ct = default);
    Task<Result<List<AuthZeroRoleResponse>>> GetUserRolesAsync(string auth0UserId, CancellationToken ct = default);
}

public sealed class AuthZeroAPIManagement : IAuthZeroManagementClient
{
    private const string ClientName = "Auth0Management";
    private const int PerPage = 100;

    private readonly IHttpClientFactory _httpFactory;
    private readonly AuthZeroOptions _options;
    private readonly IAuthZeroManagementTokenProvider _tokenProvider;
    private readonly ILogger<AuthZeroAPIManagement> _logger;

    public AuthZeroAPIManagement(
        IHttpClientFactory httpFactory,
        IOptions<AuthZeroOptions> options,
        IAuthZeroManagementTokenProvider tokenProvider,
        ILogger<AuthZeroAPIManagement> logger)
    {
        _httpFactory = httpFactory;
        _options = options.Value;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public async Task<Result<AuthZeroBodyResponse>> PatchUserAsync(
        string auth0UserId,
        object payload,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<AuthZeroBodyResponse>.BadRequest("auth0_user_id_required");

        var tokenRes = await _tokenProvider.GetTokenAsync(ct);
        if (!tokenRes.IsSuccess || string.IsNullOrWhiteSpace(tokenRes.Data))
            return Result<AuthZeroBodyResponse>.Unauthorized(tokenRes.Message ?? "auth0_mgmt_token_error");

        var encodedUserId = Uri.EscapeDataString(auth0UserId);
        var url = $"{ManagementBaseUrl}/users/{encodedUserId}";

        var httpClient = CreateAuthorizedClient(tokenRes.Data);

        using var request = new HttpRequestMessage(HttpMethod.Patch, url);
        request.Content = JsonContent.Create(payload);

        using var response = await httpClient.SendAsync(request, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return MapAuth0ManagementError<AuthZeroBodyResponse>(
                response.StatusCode,
                responseContent,
                "patch_user",
                auth0UserId);
        }

        var mappedDetails = AuthZeroDetailsMapper.Build("Auth0", responseContent);
        return Result<AuthZeroBodyResponse>.Success(mappedDetails, "auth0_user_updated");
    }

    public async Task<Result<AuthZeroBodyResponse>> DeleteUserAsync(
        string auth0UserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<AuthZeroBodyResponse>.BadRequest("auth0_user_id_required");

        var tokenRes = await _tokenProvider.GetTokenAsync(ct);
        if (!tokenRes.IsSuccess || string.IsNullOrWhiteSpace(tokenRes.Data))
            return Result<AuthZeroBodyResponse>.Unauthorized(tokenRes.Message ?? "auth0_mgmt_token_error");

        var encodedUserId = Uri.EscapeDataString(auth0UserId);
        var url = $"{ManagementBaseUrl}/users/{encodedUserId}";

        var httpClient = CreateAuthorizedClient(tokenRes.Data);

        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        using var response = await httpClient.SendAsync(request, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return MapAuth0ManagementError<AuthZeroBodyResponse>(
                response.StatusCode,
                responseContent,
                "delete_user",
                auth0UserId);
        }

        return Result<AuthZeroBodyResponse>.NoContent("auth0_user_deleted");
    }

    public async Task<Result<List<AuthZeroUserSlim>>> GetUsersInRoleAsync(
        string roleId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(roleId))
            return Result<List<AuthZeroUserSlim>>.BadRequest("auth0_role_id_required");

        var tokenRes = await _tokenProvider.GetTokenAsync(ct);
        if (!tokenRes.IsSuccess || string.IsNullOrWhiteSpace(tokenRes.Data))
        {
            return new Result<List<AuthZeroUserSlim>>(
                false,
                tokenRes.Status,
                null,
                tokenRes.Message ?? "auth0_mgmt_token_error");
        }

        var httpClient = CreateAuthorizedClient(tokenRes.Data);
        var encodedRoleId = Uri.EscapeDataString(roleId);

        var all = new List<AuthZeroUserSlim>();
        var page = 0;

        while (true)
        {
            var url = $"{ManagementBaseUrl}/roles/{encodedRoleId}/users?per_page={PerPage}&page={page}";

            using var response = await httpClient.GetAsync(url, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return MapAuth0ManagementError<List<AuthZeroUserSlim>>(
                    response.StatusCode,
                    body,
                    "get_users_in_role");
            }

            var users = await response.Content.ReadFromJsonAsync<List<AuthZeroUserResponse>>(cancellationToken: ct)
                        ?? new List<AuthZeroUserResponse>();

            all.AddRange(users.Select(MapSlimUser));

            if (users.Count < PerPage)
                break;

            page++;
        }

        return Result<List<AuthZeroUserSlim>>.Success(all);
    }

    public async Task<Result<List<AuthZeroRoleResponse>>> GetRolesAsync(CancellationToken ct = default)
    {
        var tokenRes = await _tokenProvider.GetTokenAsync(ct);
        if (!tokenRes.IsSuccess || string.IsNullOrWhiteSpace(tokenRes.Data))
            return Result<List<AuthZeroRoleResponse>>.Unauthorized(tokenRes.Message ?? "auth0_mgmt_token_error");

        var httpClient = CreateAuthorizedClient(tokenRes.Data);
        var url = $"{ManagementBaseUrl}/roles?per_page={PerPage}&page=0";

        using var response = await httpClient.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return MapAuth0ManagementError<List<AuthZeroRoleResponse>>(
                response.StatusCode,
                body,
                "get_roles");
        }

        var roles = await response.Content.ReadFromJsonAsync<List<AuthZeroRoleResponse>>(cancellationToken: ct)
                    ?? new List<AuthZeroRoleResponse>();

        return Result<List<AuthZeroRoleResponse>>.Success(roles);
    }

    public async Task<Result<List<AuthZeroRoleResponse>>> GetUserRolesAsync(
        string auth0UserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<List<AuthZeroRoleResponse>>.BadRequest("auth0_user_id_required");

        var tokenRes = await _tokenProvider.GetTokenAsync(ct);
        if (!tokenRes.IsSuccess || string.IsNullOrWhiteSpace(tokenRes.Data))
            return Result<List<AuthZeroRoleResponse>>.Unauthorized(tokenRes.Message ?? "auth0_mgmt_token_error");

        var httpClient = CreateAuthorizedClient(tokenRes.Data);
        var encodedUserId = Uri.EscapeDataString(auth0UserId);
        var url = $"{ManagementBaseUrl}/users/{encodedUserId}/roles?per_page={PerPage}&page=0";

        using var response = await httpClient.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return MapAuth0ManagementError<List<AuthZeroRoleResponse>>(
                response.StatusCode,
                body,
                "get_user_roles",
                auth0UserId);
        }

        var roles = await response.Content.ReadFromJsonAsync<List<AuthZeroRoleResponse>>(cancellationToken: ct)
                    ?? new List<AuthZeroRoleResponse>();

        return Result<List<AuthZeroRoleResponse>>.Success(roles);
    }

    public async Task<Result<string>> AssignRolesToUserAsync(
        string auth0UserId,
        IReadOnlyCollection<string> roleIds,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<string>.BadRequest("auth0_user_id_required");

        if (roleIds is null || roleIds.Count == 0)
            return Result<string>.BadRequest("role_ids_required");

        var tokenRes = await _tokenProvider.GetTokenAsync(ct);
        if (!tokenRes.IsSuccess || string.IsNullOrWhiteSpace(tokenRes.Data))
            return Result<string>.Unauthorized(tokenRes.Message ?? "auth0_mgmt_token_error");

        var httpClient = CreateAuthorizedClient(tokenRes.Data);
        var encodedUserId = Uri.EscapeDataString(auth0UserId);
        var url = $"{ManagementBaseUrl}/users/{encodedUserId}/roles";

        using var response = await httpClient.PostAsJsonAsync(url, new { roles = roleIds }, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return MapAuth0ManagementError<string>(
                response.StatusCode,
                body,
                "assign_roles",
                auth0UserId);
        }

        return Result<string>.NoContent("auth0_roles_assigned");
    }

    public async Task<Result<string>> RemoveRolesFromUserAsync(
        string auth0UserId,
        IReadOnlyCollection<string> roleIds,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<string>.BadRequest("auth0_user_id_required");

        if (roleIds is null || roleIds.Count == 0)
            return Result<string>.BadRequest("role_ids_required");

        var tokenRes = await _tokenProvider.GetTokenAsync(ct);
        if (!tokenRes.IsSuccess || string.IsNullOrWhiteSpace(tokenRes.Data))
            return Result<string>.Unauthorized(tokenRes.Message ?? "auth0_mgmt_token_error");

        var httpClient = CreateAuthorizedClient(tokenRes.Data);
        var encodedUserId = Uri.EscapeDataString(auth0UserId);
        var url = $"{ManagementBaseUrl}/users/{encodedUserId}/roles";

        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Content = JsonContent.Create(new { roles = roleIds });

        using var response = await httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return MapAuth0ManagementError<string>(
                response.StatusCode,
                body,
                "remove_roles",
                auth0UserId);
        }

        return Result<string>.NoContent("auth0_roles_removed");
    }

    private string ManagementBaseUrl => $"https://{_options.Domain}/api/v2";

    private HttpClient CreateAuthorizedClient(string token)
    {
        var httpClient = _httpFactory.CreateClient(ClientName);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return httpClient;
    }

    private static AuthZeroUserSlim MapSlimUser(AuthZeroUserResponse user)
    {
        return new AuthZeroUserSlim
        {
            UserId = user.UserId,
            Email = user.Email,
            Nickname = user.Nickname,
            Name = user.Name,
            CreatedAt = user.CreatedAt,
            Roles = new List<string>()
        };
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
                Result<T>.Unauthorized($"{operationName}_unauthorized"),

            HttpStatusCode.Forbidden =>
                Result<T>.Unauthorized($"{operationName}_forbidden"),

            HttpStatusCode.NotFound =>
                Result<T>.NotFound($"{operationName}_not_found"),

            HttpStatusCode.Conflict =>
                Result<T>.Conflict($"{operationName}_conflict"),

            HttpStatusCode.BadRequest =>
                Result<T>.BadRequest($"{operationName}_bad_request"),

            _ when (int)statusCode is >= 500 and < 600 =>
                Result<T>.InternalServerError($"{operationName}_provider_unavailable"),

            _ =>
                Result<T>.BadRequest($"{operationName}_provider_error")
        };
    }
}