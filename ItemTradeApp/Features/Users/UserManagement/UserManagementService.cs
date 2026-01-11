using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Users.UserManagement.DTOs;
using ItemTradeApp.Features.Users.UserManagement.DTOs.Response;
using ItemTradeApp.Users.AuthZeroCommunication;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.Features.Users.UserManagement;

public interface IUserManagementService
{
    Task<Result<string>> UpdateUserAsync(
        UpdateUserRequest request,
        CancellationToken ct = default);

    Task<Result<string>> DeleteUserAsync(
        string auth0UserId,
        CancellationToken ct = default);

    Task<Result<UserListPagedResponse>> GetUsersAsync(UserListQuery query, CancellationToken ct = default);
}

public class UserManagementService(
        IAuthZeroManagementClient authZeroManagementClient,
        IUserManagementRepository userManagementRepository,
        IOptions<AuthZeroOptions> auth0Options) : IUserManagementService
    {
       public async Task<Result<string>> UpdateUserAsync(UpdateUserRequest request, CancellationToken ct = default)
{
    if (request is null)
        return Result<string>.BadRequest("body_required");

    if (string.IsNullOrWhiteSpace(request.AuthZeroUserId))
        return Result<string>.BadRequest("auth0_user_id_required");

    var trimmedAuth0UserId = request.AuthZeroUserId.StartsWith("auth0|", StringComparison.Ordinal)
        ? request.AuthZeroUserId["auth0|".Length..]
        : request.AuthZeroUserId;

    var user = await userManagementRepository.GetUserByAuth0IdAsync(trimmedAuth0UserId, ct);
    if (user is null)
        return Result<string>.NotFound("user_not_found_local_db");

    var hasEmailChange =
        !string.IsNullOrWhiteSpace(request.Email) &&
        !string.Equals(request.Email, user.Email, StringComparison.OrdinalIgnoreCase);
    var hasPasswordChange = !string.IsNullOrWhiteSpace(request.NewPassword);
    var hasRolesChange = request.Roles is not null;
    var hasNicknameChange = !string.IsNullOrWhiteSpace(request.Nickname);

    if (!hasEmailChange && !hasPasswordChange && !hasRolesChange && !hasNicknameChange)
        return Result<string>.NoContent("no_changes");

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

    if (hasRolesChange)
    {
        payload["app_metadata"] = new Dictionary<string, object?>
        {
            ["roles"] = request.Roles
        };
    }

    if (hasNicknameChange)
    {
        payload["nickname"] = request.Nickname!;
        payload["name"] = request.Nickname!;
    }
    var fullAuthZeroUserId = request.AuthZeroUserId.StartsWith("auth0|", StringComparison.Ordinal)
        ? request.AuthZeroUserId
        :  "auth0|" + request.AuthZeroUserId;
    var auth0Result = await authZeroManagementClient.PatchUserAsync(fullAuthZeroUserId, payload, ct);

    if (!auth0Result.IsSuccess)
    {
        var msg =
            auth0Result.Data?.Details?.ErrorDescription
            ?? auth0Result.Data?.Details?.Text
            ?? auth0Result.Message
            ?? "auth0_admin_update_user_failed";

        return new Result<string>(
            isSuccess: false,
            status: auth0Result.Status,
            data: default,
            message: msg);
    }

    if (hasEmailChange)
        user.Email = request.Email!;

    if (hasNicknameChange)
    {
        user.ProfileInfo.Nickname = request.Nickname!;
    }

    await userManagementRepository.UpdateUserAsync(user, ct);

    return Result<string>.Success(null, "user_updated");
}


public async Task<Result<string>> DeleteUserAsync(string auth0UserId, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(auth0UserId))
        return Result<string>.BadRequest("auth0_user_id_required");

    var fullAuthZeroUserId = auth0UserId.StartsWith("auth0|", StringComparison.Ordinal)
        ? auth0UserId
        : "auth0|" + auth0UserId;

    var auth0Result = await authZeroManagementClient.DeleteUserAsync(fullAuthZeroUserId, ct);

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

    await userManagementRepository.DeleteUserByAuth0IdAsync(auth0UserId, ct);

    return Result<string>.NoContent("user_deleted");
}

        
        private const string MiddlemanRoleName = "Middleman";

        private static string TrimAuth0Prefix(string auth0UserId)
            => auth0UserId.StartsWith("auth0|", StringComparison.Ordinal)
                ? auth0UserId["auth0|".Length..]
                : auth0UserId;

        public async Task<Result<UserListPagedResponse>> GetUsersAsync(UserListQuery query,
            CancellationToken ct = default)
        {
            if (query is null)
                return Result<UserListPagedResponse>.BadRequest("body_required");

            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize <= 0 ? 10 : query.PageSize;

            var auth0AllRes = await authZeroManagementClient.GetAllUsersAsync(ct);
            if (!auth0AllRes.IsSuccess || auth0AllRes.Data is null)
            {
                return new Result<UserListPagedResponse>(
                    isSuccess: false,
                    status: auth0AllRes.Status,
                    data: default,
                    message: auth0AllRes.Message ?? "auth0_get_all_users_failed");
            }

            var rolesByLocalAuth0Id = auth0AllRes.Data
                .GroupBy(u => TrimAuth0Prefix(u.UserId), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().Roles ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase);

            IReadOnlyCollection<string>? auth0RoleFilter = null;
            if (!string.IsNullOrWhiteSpace(query.Role))
            {
                var role = query.Role.Trim();
                auth0RoleFilter = rolesByLocalAuth0Id
                    .Where(kv => kv.Value.Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase)))
                    .Select(kv => kv.Key)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (auth0RoleFilter.Count == 0)
                {
                    return Result<UserListPagedResponse>.Success(new UserListPagedResponse
                    {
                        Page = page,
                        PageSize = pageSize,
                        TotalCount = 0,
                        TotalPages = 1,
                        Elements = new List<UserListItemDTO>(),
                        RegisteredLastMonthCount =
                            0, 
                        MiddlemenCount = 0
                    }, "users_query");
                }
            }

            var middlemanAuth0Ids = rolesByLocalAuth0Id
                .Where(kv => kv.Value.Any(r => r.Equals(MiddlemanRoleName, StringComparison.OrdinalIgnoreCase)))
                .Select(kv => kv.Key)
                .ToArray();

            var repoQuery = new UserListQuery
            {
                Page = page,
                PageSize = pageSize,
                SearchText = query.SearchText,
                OrderBy = query.OrderBy,
                RegisteredFrom = query.RegisteredFrom,
                RegisteredTo = query.RegisteredTo,
                Role = query.Role
            };

            (List<UserListItemDTO> items, int totalCount, int registeredLastMonthCount, int middlemenCount) tuple;

            try
            {
                tuple = await userManagementRepository.GetUsersPageWithStatsAsync(
                    repoQuery,
                    auth0RoleFilter,
                    middlemanAuth0Ids,
                    ct);
            }
            catch (Exception ex)
            {
                return new Result<UserListPagedResponse>(
                    isSuccess: false,
                    status: ResultStatus.InternalServerError,
                    data: default,
                    message: ex.Message);
            }

            var items = tuple.items;
            var totalCount = tuple.totalCount;

            foreach (var item in items)
            {
                var localId = TrimAuth0Prefix(item.Auth0UserId);

                item.Roles = rolesByLocalAuth0Id.TryGetValue(localId, out var roles)
                    ? roles
                    : new List<string>();
            }

            var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);

            var response = new UserListPagedResponse
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                Elements = items,
                RegisteredLastMonthCount = tuple.registeredLastMonthCount,
                MiddlemenCount = tuple.middlemenCount
            };

            return Result<UserListPagedResponse>.Success(response, "users_query");
        }
    }
    