using ItemTradeApp.AuthZeroCommunication;
using ItemTradeApp.Features.UsersFeature.UserManagement.DTOs;
using ItemTradeApp.Features.UsersFeature.UserSettings;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.Features.UsersFeature.UserManagement;

public interface IUserManagementService
{
    Task<Result<string>> UpdateUserAsync(
        UpdateUserRequest request,
        CancellationToken ct = default);
}
public class UserManagementService (IAuthZeroManagementClient authZeroManagementClient,
    IUserSettingsRepository userSettingsRepository, IOptions<Auth0Options> auth0Options) : IUserManagementService
{
     public async Task<Result<string>> UpdateUserAsync(
        UpdateUserRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.AuthZeroUserId))
        {
            return Result<string>.BadRequest("auth0_user_id_required");
        }

        if (request is null)
        {
            return Result<string>.BadRequest("body_required");
        }
        string trimmedAuth0UserId = request.AuthZeroUserId.StartsWith("auth0|")
            ? request.AuthZeroUserId.Substring("auth0|".Length)
            : request.AuthZeroUserId;
        var user = await userSettingsRepository.GetUserByAuth0IdAsync(trimmedAuth0UserId, ct);
        if (user is null)
        {
            return Result<string>.NotFound("user_not_found_local_db");
        }

        var hasEmailChange    = !string.IsNullOrWhiteSpace(request.Email);
        var hasPasswordChange = !string.IsNullOrWhiteSpace(request.NewPassword);
        var hasRolesChange    = request.Roles is { Count: > 0 };

        if (!hasEmailChange && !hasPasswordChange && !hasRolesChange)
        {
            return Result<string>.NoContent("no_changes");
        }

        var payload = new Dictionary<string, object>();

        if (hasEmailChange)
        {
            payload["email"]          = request.Email!;
            payload["verify_email"]   = true;
            payload["email_verified"] = false;
        }

        if (hasPasswordChange)
        {
            payload["password"]   = request.NewPassword!;
            payload["connection"] = auth0Options.Value.Realm;
        }

        if (hasRolesChange)
        {
            payload["app_metadata"] = new
            {
                roles = request.Roles
            };
        }
        var auth0Result = await authZeroManagementClient.PatchUserAsync(request.AuthZeroUserId, payload, ct);

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
        {
            user.Email = request.Email!;
        }
        

        await userSettingsRepository.UpdateUserAsync(user, ct);

        return Result<string>.Success(null, "admin_user_updated");
    }
}