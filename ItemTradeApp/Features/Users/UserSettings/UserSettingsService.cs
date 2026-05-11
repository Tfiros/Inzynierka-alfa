using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration;
using ItemTradeApp.Features.Users.UserSettings.DTOs;
using ItemTradeApp.Users.AuthZeroCommunication;

namespace ItemTradeApp.Features.Users.UserSettings;

public interface IUserSettingsService
{
    Task<Result<string>> UpdateSensitiveDataAsync(
        string auth0UserId,
        UserDataUpdateRequest request,
        CancellationToken ct = default);
    
    Task<Result<UserSecurityInfoResponse>> GetSecurityProfileInfoAsync(int userId, CancellationToken ct = default);
}
public sealed class UserSettingsService(IUserSettingsRepository userSettingsRepository,
    IAuthZeroManagementClient authZeroApiClient) : IUserSettingsService
{
    public async Task<Result<string>> UpdateSensitiveDataAsync(string auth0UserId, UserDataUpdateRequest request, CancellationToken ct = default)
    {
         if (request is null)
        {
            return Result<string>.BadRequest("Body is required.");
        }

        if (string.IsNullOrWhiteSpace(auth0UserId))
        {
            return Result<string>.Unauthorized("Missing auth0 user id (sub claim).");
        }
        string trimmedAuth0UserId = auth0UserId.StartsWith("auth0|")
            ? auth0UserId.Substring("auth0|".Length)
            : auth0UserId;
        var user = await userSettingsRepository.GetUserByAuth0IdAsync(trimmedAuth0UserId, ct);
        if (user is null)
        {
            return Result<string>.NotFound("user_not_found");
        }

        var emailChanged = false;
        var dobChanged   = false;

        if (!string.IsNullOrWhiteSpace(request.Email) &&
            !string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            emailChanged = true;
        }

        if (request.DateOfBirth.HasValue &&
            user.DateOfBirth != request.DateOfBirth.Value)
        {
            dobChanged = true;
        }

        if (!emailChanged && !dobChanged)
        {
            return Result<string>.NoContent("no_changes");
        }

        if (emailChanged)
        {
            var payload = new
            {
                email          = request.Email,
                verify_email   = true,
                email_verified = false
            };

            var auth0Result = await authZeroApiClient.PatchUserAsync(auth0UserId, payload, ct);

            if (!auth0Result.IsSuccess)
            {
                var msg =
                    auth0Result.Data?.Details?.ErrorDescription
                    ?? auth0Result.Data?.Details?.Text
                    ?? auth0Result.Message
                    ?? "auth0_update_user_failed";

                return new Result<string>(
                    isSuccess: false,
                    status: auth0Result.Status,
                    data: default,
                    message: msg);
            }
        }

        if (emailChanged)
        {
            user.Email = request.Email!;
        }

        if (dobChanged && request.DateOfBirth.HasValue)
        {
            user.DateOfBirth = request.DateOfBirth.Value;
        }

        await userSettingsRepository.UpdateUserAsync(user, ct);

        return Result<string>.Success("user_sensitive_data_updated");
    }
    public async Task<Result<UserSecurityInfoResponse>> GetSecurityProfileInfoAsync(int userId, CancellationToken ct = default)
    {
        var user = await userSettingsRepository.GetUserWithProfileInfoByUserIdAsync(userId, ct);
        if (user is null || user.ProfileInfo is null)
        {
            return Result<UserSecurityInfoResponse>.NotFound(
                "user_or_profile_info_not_found: User or profile info not found");
        }
        

        var dto = new UserSecurityInfoResponse(
            user.ID,
            user.DateOfBirth,
            user.Email
        );

        return Result<UserSecurityInfoResponse>.Success(dto);
    }
}