using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Shared;
using ItemTradeApp.Features.Shared.TokenEscrow;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration;
using ItemTradeApp.Features.Users.UserManagement.DTOs.Request;
using ItemTradeApp.Features.Users.UserManagement.DTOs.Response;
using ItemTradeApp.Persistence;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.Features.Users.UserManagement;

public interface IUserManagementService
{
    Task<Result<string>> UpdateUserAsync(UpdateUserRequest request, CancellationToken ct = default);
    Task<Result<string>> DeleteUserAsync(string auth0UserId, CancellationToken ct = default);
    Task<Result<UserListPagedResponse>> GetUsersAsync(UserListQuery query, CancellationToken ct = default);
    Task<Result<UserDetailsResponse>> GetUserDetailsAsync(string auth0UserId, CancellationToken ct = default);
}

public sealed class UserManagementService(
    IAuthZeroManagementClient authZeroManagementClient,
    IUserManagementRepository userManagementRepository,
    ITokenEscrow tokenEscrow,
    IUnitOfWork unitOfWork,
    IOptions<AuthZeroOptions> auth0Options) : IUserManagementService
{
    private const string MiddlemanRoleName = "Middleman";

    private static readonly int[] OfferStatusesToCancel =
    [
        (int)OfferStatuses.Active,
        (int)OfferStatuses.InRealization,
        (int)OfferStatuses.Expired
    ];

    private static readonly int[] TradeStatusesToFail =
    [
        (int)TradeStatuses.New,
        (int)TradeStatuses.InRealization
    ];

    public async Task<Result<string>> UpdateUserAsync(UpdateUserRequest request, CancellationToken ct = default)
    {
        if (request is null)
            return Result<string>.BadRequest("body_required");

        if (string.IsNullOrWhiteSpace(request.AuthZeroUserId))
            return Result<string>.BadRequest("auth0_user_id_required");

        var trimmedAuth0UserId = Auth0IdHandler.Trim(request.AuthZeroUserId);
        
        var user = await userManagementRepository.GetUserByAuth0IdAsync(trimmedAuth0UserId, ct);
        if (user is null)
            return Result<string>.NotFound("user_not_found_local_db");

        var hasEmailChange =
            !string.IsNullOrWhiteSpace(request.Email) &&
            !string.Equals(request.Email, user.Email, StringComparison.OrdinalIgnoreCase);

        var hasPasswordChange = !string.IsNullOrWhiteSpace(request.NewPassword);
        var hasNicknameChange = !string.IsNullOrWhiteSpace(request.Nickname);
        var hasTokensChange =
            request.Tokens is not null &&
            request.Tokens.Value != user.Tokens;
        var hasDescriptionChange =
            request.ProfileDescription is not null &&
            !string.Equals(
                request.ProfileDescription,
                user.ProfileInfo.Description,
                StringComparison.Ordinal);

        var hasRolesChange = request.Roles is not null;

        if (!hasEmailChange &&
            !hasPasswordChange &&
            !hasNicknameChange &&
            !hasDescriptionChange &&
            !hasRolesChange &&
            !hasTokensChange)
        {
            return Result<string>.NoContent("no_changes");
        }
        
        var fullAuth0UserId = Auth0IdHandler.EnsureAuth0WithPrefix(request.AuthZeroUserId);

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
                return new Result<string>(false, auth0PatchRes.Status, default, auth0PatchRes.Message ?? "auth0_admin_update_user_failed");
        }

        if (hasRolesChange)
        {
            var rolesRes = await authZeroManagementClient.GetRolesAsync(ct);
            if (!rolesRes.IsSuccess || rolesRes.Data is null)
                return new Result<string>(false, rolesRes.Status, default, rolesRes.Message ?? "auth0_get_roles_failed");

            var roleIdByName = rolesRes.Data
                .Where(r => !string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(r.Id))
                .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

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
        if (hasTokensChange)
            user.Tokens = request.Tokens!.Value;
        if (user.ProfileInfo is not null)
        {
            if (hasNicknameChange)
                user.ProfileInfo.Nickname = request.Nickname!;

            if (hasDescriptionChange)
                user.ProfileInfo.Description = request.ProfileDescription!;
        }

        await unitOfWork.SaveChangesAsync(ct);

        return Result<string>.Success(null, "user_updated");
    }

    public async Task<Result<string>> DeleteUserAsync(string auth0UserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<string>.BadRequest("auth0_user_id_required");
        
        var trimmedAuth0UserId = Auth0IdHandler.Trim(auth0UserId);
        var fullAuth0UserId = Auth0IdHandler.EnsureAuth0WithPrefix(auth0UserId);

        var user = await userManagementRepository.GetUserByAuth0IdAsync(trimmedAuth0UserId, ct);
        if (user is null)
            return Result<string>.NotFound("user_not_found_local_db");

        await using var tx = await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            
            var offersToBeRefunded = await userManagementRepository.GetActiveUserOffersForRefundAsync(user.ID, ct);
            var ownCoToBeRefunded = await userManagementRepository.GetOwnUserCounterOffersForRefundAsync(user.ID, ct);
            var othersCoToBeRefunded =
                await userManagementRepository.GetReceivedUserCounterOffersForRefundAsync(user.ID, ct);
            var tradesToBeRefunded = await userManagementRepository.GetTradesInProgressForRefundAsync(user.ID, ct);

            foreach (var offer in offersToBeRefunded)
            {
                if (!await tokenEscrow.TryReleaseOwnEscrowAsync(user.ID, offer.TokensOffered, ct))
                {
                    await tx.RollbackAsync(ct);
                    return Result<string>.BadRequest("release_own_offer_escrow_failed");
                }
            }
            
            foreach (var counterOffer in ownCoToBeRefunded)
            {
                if (!await tokenEscrow.TryReleaseOwnEscrowAsync(counterOffer.OwnerUserId, counterOffer.TokensOffered, ct))
                {
                    await tx.RollbackAsync(ct);
                    return Result<string>.BadRequest("release_own_counteroffer_escrow_failed");
                }
            }
            
            foreach (var counterOffer in othersCoToBeRefunded)
            {
                if (!await tokenEscrow.TryReleaseOwnEscrowAsync(counterOffer.OwnerUserId, counterOffer.TokensOffered, ct))
                {
                    await tx.RollbackAsync(ct);
                    return Result<string>.BadRequest("release_received_counteroffer_escrow_failed");
                }
            }
            
            foreach (var trade in tradesToBeRefunded)
            {

                if (trade.TokensOffered > 0)
                {
                    if (!await tokenEscrow.TryRefundEscrowToOtherAsync(trade.CustomerId, trade.SellerId,
                            trade.TokensOffered, ct))
                    {
                        await tx.RollbackAsync(ct);
                        return Result<string>.BadRequest("refund_trade_offered_escrow_failed");
                    }
                }
                
                if (trade.TokensWanted > 0)
                {
                    if (!await tokenEscrow.TryRefundEscrowToOtherAsync(trade.SellerId, trade.CustomerId,
                            trade.TokensWanted, ct))
                    {
                        await tx.RollbackAsync(ct);
                        return Result<string>.BadRequest("refund_trade_wanted_escrow_failed");
                    }
                }
            }
            
            await userManagementRepository.UpdateOfferStatusesForUserAsync(
                user.ID,
                OfferStatusesToCancel,
                (int)OfferStatuses.Canceled,
                ct);

            await userManagementRepository.UpdateTradeStatusesForUserAsync(
                user.ID,
                TradeStatusesToFail,
                (int)TradeStatuses.Failed,
                ct);

            await userManagementRepository.UpdateCounterOfferStatusesForUserAsync(
                user.ID,
                (int)CounterOfferStatuses.Denied,
                ct);
            await userManagementRepository.DenyReceivedUserCounterOffersForRefundAsync(user.ID, ct);

            userManagementRepository.SoftDeleteUser(user);

            await unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            var auth0Result = await authZeroManagementClient.DeleteUserAsync(fullAuth0UserId, ct);
            if (!auth0Result.IsSuccess)
                return new Result<string>(false, auth0Result.Status, default, auth0Result.Message ?? "auth0_admin_delete_user_failed");
            return Result<string>.NoContent("user_deleted");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Result<UserListPagedResponse>> GetUsersAsync(UserListQuery query, CancellationToken ct = default)
    {
        if (query is null)
            return Result<UserListPagedResponse>.BadRequest("body_required");

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 10 : query.PageSize;

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

        var roleIdByNameRes = await GetRoleIdByNameAsync(ct);
        if (!roleIdByNameRes.IsSuccess || roleIdByNameRes.Data is null)
            return FailPaged(roleIdByNameRes.Status, roleIdByNameRes.Message ?? "auth0_get_roles_failed");

        var middlemenIdsRes = await GetMiddlemanAuth0IdsAsync(roleIdByNameRes.Data, ct);
        if (!middlemenIdsRes.IsSuccess || middlemenIdsRes.Data is null)
            return FailPaged(middlemenIdsRes.Status, middlemenIdsRes.Message ?? "auth0_get_users_in_role_failed");

        var (items, totalCount, registeredLastMonthCount, middlemenCount, totalUsers) =
            await userManagementRepository.GetUsersPageWithStatsAsync(
                repoQuery,
                middlemenIdsRes.Data,
                ct);

        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result<UserListPagedResponse>.Success(new UserListPagedResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            TotalUsers = totalUsers,
            Elements = items,
            RegisteredLastMonthCount = registeredLastMonthCount,
            MiddlemenCount = middlemenCount
        }, "users_query");
    }

    public async Task<Result<UserDetailsResponse>> GetUserDetailsAsync(
        string auth0UserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Result<UserDetailsResponse>.BadRequest("invalid_auth0_user_id");

        var user = await userManagementRepository
            .GetUserByAuth0IdAsync(auth0UserId, ct);

        if (user is null)
            return Result<UserDetailsResponse>.NotFound("user_not_found");

        var rolesRes = await authZeroManagementClient
            .GetUserRolesAsync(Auth0IdHandler.EnsureAuth0WithPrefix(auth0UserId), ct);

        if (!rolesRes.IsSuccess || rolesRes.Data is null)
        {
            return new Result<UserDetailsResponse>(
                false,
                rolesRes.Status,
                default,
                rolesRes.Message ?? "auth0_get_user_roles_failed");
        }

        var roles = rolesRes.Data
            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .Select(r => r.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Result<UserDetailsResponse>.Success(
            new UserDetailsResponse(
                user.ProfileInfo!.Description,
                user.Tokens,
                roles
            )
        );
    }

    private async Task<Result<Dictionary<string, string>>> GetRoleIdByNameAsync(CancellationToken ct)
    {
        var rolesRes = await authZeroManagementClient.GetRolesAsync(ct);
        if (!rolesRes.IsSuccess || rolesRes.Data is null)
            return new Result<Dictionary<string, string>>(false, rolesRes.Status, null, rolesRes.Message ?? "auth0_get_roles_failed");

        var dict = rolesRes.Data
            .Where(r => !string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(r.Id))
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        return Result<Dictionary<string, string>>.Success(dict);
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
            .Select(u => Auth0IdHandler.Trim(u.UserId))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return Result<string[]>.Success(ids);
    }
    private static Result<UserListPagedResponse> EmptyPaged(int page, int pageSize)
        => Result<UserListPagedResponse>.Success(new UserListPagedResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = 0,
            TotalPages = 1,
            Elements = new List<UserListItemDTO>(),
            RegisteredLastMonthCount = 0,
            MiddlemenCount = 0
        }, "users_query");

    private static Result<UserListPagedResponse> FailPaged(ResultStatus status, string message)
        => new(false, status, default, message);
}