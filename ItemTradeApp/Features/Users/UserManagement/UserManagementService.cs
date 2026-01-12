using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Users.UserManagement.DTOs;
using ItemTradeApp.Features.Users.UserManagement.DTOs.Response;
using ItemTradeApp.Users.AuthZeroCommunication;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.Features.Users.UserManagement;

public interface IUserManagementService
{
    Task<Result<string>> UpdateUserAsync(UpdateUserRequest request, CancellationToken ct = default);
    Task<Result<string>> DeleteUserAsync(string auth0UserId, CancellationToken ct = default);
    Task<Result<UserListPagedResponse>> GetUsersAsync(UserListQuery query, CancellationToken ct = default);
}

public sealed class UserManagementService(
    IAuthZeroManagementClient authZeroManagementClient,
    IUserManagementRepository userManagementRepository,
    IOptions<AuthZeroOptions> auth0Options) : IUserManagementService
{
    private const string MiddlemanRoleName = "Middleman";

    private static string EnsureAuth0Prefix(string auth0UserId)
        => auth0UserId.StartsWith("auth0|", StringComparison.Ordinal)
            ? auth0UserId
            : "auth0|" + auth0UserId;

    private static string TrimAuth0Prefix(string auth0UserId)
        => auth0UserId.StartsWith("auth0|", StringComparison.Ordinal)
            ? auth0UserId["auth0|".Length..]
            : auth0UserId;

    public async Task<Result<string>> UpdateUserAsync(UpdateUserRequest request, CancellationToken ct = default)
    {
        if (request is null)
            return Result<string>.BadRequest("body_required");

        if (string.IsNullOrWhiteSpace(request.AuthZeroUserId))
            return Result<string>.BadRequest("auth0_user_id_required");

        var trimmedAuth0UserId = TrimAuth0Prefix(request.AuthZeroUserId);

        var user = await userManagementRepository.GetUserByAuth0IdAsync(trimmedAuth0UserId, ct);
        if (user is null)
            return Result<string>.NotFound("user_not_found_local_db");

        var hasEmailChange =
            !string.IsNullOrWhiteSpace(request.Email) &&
            !string.Equals(request.Email, user.Email, StringComparison.OrdinalIgnoreCase);

        var hasPasswordChange = !string.IsNullOrWhiteSpace(request.NewPassword);
        var hasNicknameChange = !string.IsNullOrWhiteSpace(request.Nickname);
        var hasRolesChange = request.Roles is not null;

        if (!hasEmailChange && !hasPasswordChange && !hasNicknameChange && !hasRolesChange)
            return Result<string>.NoContent("no_changes");

        var fullAuth0UserId = EnsureAuth0Prefix(request.AuthZeroUserId);

        if (hasEmailChange || hasPasswordChange || hasNicknameChange)
        {
            var payload = new Dictionary<string, object>();

            if (hasEmailChange)
            {
                payload["email"] = request.Email!;
                payload["verify_email"] = true;
                payload["email_verified"] = false;
            }

            if (hasPasswordChange)
            {
                payload["password"] = request.NewPassword!;
                payload["connection"] = auth0Options.Value.Realm;
            }

            if (hasNicknameChange)
            {
                payload["nickname"] = request.Nickname!;
                payload["name"] = request.Nickname!;
            }

            var auth0PatchRes = await authZeroManagementClient.PatchUserAsync(fullAuth0UserId, payload, ct);
            if (!auth0PatchRes.IsSuccess)
            {
                var msg =
                    auth0PatchRes.Data?.Details?.ErrorDescription
                    ?? auth0PatchRes.Data?.Details?.Text
                    ?? auth0PatchRes.Message
                    ?? "auth0_admin_update_user_failed";

                return new Result<string>(
                    isSuccess: false,
                    status: auth0PatchRes.Status,
                    data: default,
                    message: msg);
            }
        }

        if (hasRolesChange)
        {
            var rolesRes = await authZeroManagementClient.GetRolesAsync(ct);
            if (!rolesRes.IsSuccess || rolesRes.Data is null)
                return new Result<string>(false, rolesRes.Status, default, rolesRes.Message ?? "auth0_get_roles_failed");

            var roleIdByName = rolesRes.Data
                .Where(r => !string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(r.Id))
                .ToDictionary(r => r.Name, r => r.Id, StringComparer.OrdinalIgnoreCase);

            var requestedNames = (request.Roles ?? new List<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var unknown = requestedNames.Where(n => !roleIdByName.ContainsKey(n)).ToList();
            if (unknown.Count > 0)
                return Result<string>.BadRequest($"unknown_roles: {string.Join(", ", unknown)}");

            var requestedRoleIds = requestedNames
                .Select(n => roleIdByName[n])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var currentRes = await authZeroManagementClient.GetUserRolesAsync(fullAuth0UserId, ct);
            if (!currentRes.IsSuccess || currentRes.Data is null)
                return new Result<string>(false, currentRes.Status, default, currentRes.Message ?? "auth0_get_user_roles_failed");

            var currentRoleIds = currentRes.Data
                .Where(r => !string.IsNullOrWhiteSpace(r.Id))
                .Select(r => r.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var toAdd = requestedRoleIds.Except(currentRoleIds).ToArray();
            var toRemove = currentRoleIds.Except(requestedRoleIds).ToArray();

            if (toAdd.Length > 0)
            {
                var addRes = await authZeroManagementClient.AssignRolesToUserAsync(fullAuth0UserId, toAdd, ct);
                if (!addRes.IsSuccess)
                    return new Result<string>(false, addRes.Status, default, addRes.Message ?? "auth0_assign_roles_failed");
            }

            if (toRemove.Length > 0)
            {
                var removeRes = await authZeroManagementClient.RemoveRolesFromUserAsync(fullAuth0UserId, toRemove, ct);
                if (!removeRes.IsSuccess)
                    return new Result<string>(false, removeRes.Status, default, removeRes.Message ?? "auth0_remove_roles_failed");
            }
        }

        if (hasEmailChange)
            user.Email = request.Email!;

        if (hasNicknameChange)
            user.ProfileInfo.Nickname = request.Nickname!;

        await userManagementRepository.UpdateUserAsync(user, ct);

        return Result<string>.Success(null, "user_updated");
    }

    public async Task<Result<string>> DeleteUserAsync(string auth0UserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<string>.BadRequest("auth0_user_id_required");

        var fullAuth0UserId = EnsureAuth0Prefix(auth0UserId);

        var auth0Result = await authZeroManagementClient.DeleteUserAsync(fullAuth0UserId, ct);
        if (!auth0Result.IsSuccess)
        {
            var msg =
                auth0Result.Data?.Details?.ErrorDescription
                ?? auth0Result.Data?.Details?.Text
                ?? auth0Result.Message
                ?? "auth0_admin_delete_user_failed";

            return new Result<string>(
                isSuccess: false,
                status: auth0Result.Status,
                data: default,
                message: msg);
        }

        await userManagementRepository.DeleteUserByAuth0IdAsync(TrimAuth0Prefix(auth0UserId), ct);

        return Result<string>.NoContent("user_deleted");
    }

   public async Task<Result<UserListPagedResponse>> GetUsersAsync(UserListQuery query, CancellationToken ct = default)
{
    if (query is null)
        return Result<UserListPagedResponse>.BadRequest("body_required");

    var page = query.Page < 1 ? 1 : query.Page;
    var pageSize = query.PageSize <= 0 ? 10 : query.PageSize;

    var repoQuery = BuildRepoQuery(query, page, pageSize);

    var roleIdByNameRes = await GetRoleIdByNameAsync(ct);
    if (!roleIdByNameRes.IsSuccess || roleIdByNameRes.Data is null)
        return FailPaged(roleIdByNameRes.Status, roleIdByNameRes.Message ?? "auth0_get_roles_failed");

    var roleIdByName = roleIdByNameRes.Data;

    var roleFilterRes = await BuildAuth0RoleFilterAsync(query.Role, roleIdByName, page, pageSize, ct);
    if (!roleFilterRes.IsSuccess)
        return FailPaged(roleFilterRes.Status, roleFilterRes.Message ?? "auth0_get_users_in_role_failed");

    if (roleFilterRes.Data?.IsEmpty == true)
        return EmptyPaged(page, pageSize);

    var middlemenIdsRes = await GetMiddlemanAuth0IdsAsync(roleIdByName, ct);
    if (!middlemenIdsRes.IsSuccess || middlemenIdsRes.Data is null)
        return FailPaged(middlemenIdsRes.Status, middlemenIdsRes.Message ?? "auth0_get_users_in_role_failed");

    var tupleRes = await GetUsersTupleAsync(repoQuery, roleFilterRes.Data?.Auth0RoleFilter, middlemenIdsRes.Data, ct);
    if (!tupleRes.IsSuccess || tupleRes.Data is null)
        return FailPaged(tupleRes.Status, tupleRes.Message ?? "db_get_users_failed");

    var (items, totalCount, registeredLastMonthCount, middlemenCount) = tupleRes.Data.Value;

    var enrichRes = await EnrichUsersWithRolesAsync(items, ct);
    if (!enrichRes.IsSuccess)
        return FailPaged(enrichRes.Status, enrichRes.Message ?? "auth0_get_user_roles_failed");

    var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);

    var response = new UserListPagedResponse
    {
        Page = page,
        PageSize = pageSize,
        TotalCount = totalCount,
        TotalPages = totalPages,
        Elements = items,
        RegisteredLastMonthCount = registeredLastMonthCount,
        MiddlemenCount = middlemenCount
    };

    return Result<UserListPagedResponse>.Success(response, "users_query");
}

private static UserListQuery BuildRepoQuery(UserListQuery src, int page, int pageSize)
{
    return new UserListQuery
    {
        Page = page,
        PageSize = pageSize,
        SearchText = src.SearchText,
        OrderBy = src.OrderBy,
        RegisteredFrom = src.RegisteredFrom,
        RegisteredTo = src.RegisteredTo,
        Role = src.Role
    };
}

private async Task<Result<Dictionary<string, string>>> GetRoleIdByNameAsync(CancellationToken ct)
{
    var rolesRes = await authZeroManagementClient.GetRolesAsync(ct);
    if (!rolesRes.IsSuccess || rolesRes.Data is null)
        return new Result<Dictionary<string, string>>(false, rolesRes.Status, null, rolesRes.Message ?? "auth0_get_roles_failed");

    var dict = rolesRes.Data
        .Where(r => !string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(r.Id))
        .ToDictionary(r => r.Name!, r => r.Id!, StringComparer.OrdinalIgnoreCase);

    return Result<Dictionary<string, string>>.Success(dict);
}

private sealed record RoleFilterResult(IReadOnlyCollection<string>? Auth0RoleFilter, bool IsEmpty);

private async Task<Result<RoleFilterResult>> BuildAuth0RoleFilterAsync(
    string? roleName,
    IReadOnlyDictionary<string, string> roleIdByName,
    int page,
    int pageSize,
    CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(roleName))
        return Result<RoleFilterResult>.Success(new RoleFilterResult(null, false));

    var roleToLookup = roleName.Trim();

    if (!roleIdByName.TryGetValue(roleToLookup, out var roleId))
        return Result<RoleFilterResult>.Success(new RoleFilterResult(null, true));

    var membersRes = await authZeroManagementClient.GetUsersInRoleAsync(roleId, ct);
    if (!membersRes.IsSuccess || membersRes.Data is null)
        return new Result<RoleFilterResult>(false, membersRes.Status, null, membersRes.Message ?? "auth0_get_users_in_role_failed");

    var filter = membersRes.Data
        .Select(u => TrimAuth0Prefix(u.UserId))
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    if (filter.Count == 0)
        return Result<RoleFilterResult>.Success(new RoleFilterResult(null, true));

    return Result<RoleFilterResult>.Success(new RoleFilterResult(filter, false));
}

private async Task<Result<string[]>> GetMiddlemanAuth0IdsAsync(
    IReadOnlyDictionary<string, string> roleIdByName,
    CancellationToken ct)
{
    if (!roleIdByName.TryGetValue(MiddlemanRoleName, out var middlemanRoleId))
        return Result<string[]>.Success(Array.Empty<string>());

    var middlemenRes = await authZeroManagementClient.GetUsersInRoleAsync(middlemanRoleId, ct);
    if (!middlemenRes.IsSuccess || middlemenRes.Data is null)
        return new Result<string[]>(false, middlemenRes.Status, null, middlemenRes.Message ?? "auth0_get_users_in_role_failed");

    var ids = middlemenRes.Data
        .Select(u => TrimAuth0Prefix(u.UserId))
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToArray();

    return Result<string[]>.Success(ids);
}

private async Task<Result<(List<UserListItemDTO> items, int totalCount, int registeredLastMonthCount, int middlemenCount)?>> GetUsersTupleAsync(
    UserListQuery repoQuery,
    IReadOnlyCollection<string>? auth0RoleFilter,
    string[] middlemanAuth0Ids,
    CancellationToken ct)
{
    try
    {
        var tuple = await userManagementRepository.GetUsersPageWithStatsAsync(
            repoQuery,
            auth0RoleFilter,
            middlemanAuth0Ids,
            ct);

        return Result<(List<UserListItemDTO>, int, int, int)?>.Success(tuple);
    }
    catch (Exception ex)
    {
        return new Result<(List<UserListItemDTO>, int, int, int)?>(false, ResultStatus.InternalServerError, null, ex.Message);
    }
}

private async Task<Result<string>> EnrichUsersWithRolesAsync(List<UserListItemDTO> items, CancellationToken ct)
{
    var pageLocalIds = items
        .Select(x => TrimAuth0Prefix(x.Auth0UserId))
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (pageLocalIds.Count == 0)
    {
        foreach (var it in items) it.Roles = new List<string>();
        return Result<string>.NoContent("no_users_on_page");
    }

    var tasks = pageLocalIds.Select(async localId =>
    {
        var fullId = EnsureAuth0Prefix(localId);

        var res = await authZeroManagementClient.GetUserRolesAsync(fullId, ct);
        if (!res.IsSuccess || res.Data is null)
            return (ok: false, localId, roles: new List<string>(), status: res.Status, message: res.Message ?? "auth0_get_user_roles_failed");

        var roles = res.Data
            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .Select(r => r.Name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (ok: true, localId, roles, status: ResultStatus.Success, message: (string?)null);
    });

    var results = await Task.WhenAll(tasks);

    var err = results.FirstOrDefault(x => !x.ok);
    if (!err.ok && !string.IsNullOrWhiteSpace(err.localId))
        return new Result<string>(false, err.status, null, err.message);

    var rolesByLocalAuth0Id = results.ToDictionary(x => x.localId, x => x.roles, StringComparer.OrdinalIgnoreCase);

    foreach (var item in items)
    {
        var localId = TrimAuth0Prefix(item.Auth0UserId);
        item.Roles = rolesByLocalAuth0Id.TryGetValue(localId, out var roles) ? roles : new List<string>();
    }

    return Result<string>.NoContent("roles_enriched");
}

private Result<UserListPagedResponse> EmptyPaged(int page, int pageSize)
{
    return Result<UserListPagedResponse>.Success(new UserListPagedResponse
    {
        Page = page,
        PageSize = pageSize,
        TotalCount = 0,
        TotalPages = 1,
        Elements = new List<UserListItemDTO>(),
        RegisteredLastMonthCount = 0,
        MiddlemenCount = 0
    }, "users_query");
}

private Result<UserListPagedResponse> FailPaged(ResultStatus status, string message)
{
    return new Result<UserListPagedResponse>(false, status, default, message);
}



}
