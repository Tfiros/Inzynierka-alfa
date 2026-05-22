using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Shared.Images;
using ItemTradeApp.Persistence;
using ItemTradeApp.Features.Users.UserInfo.DTOs.Response;
using ItemTradeApp.Features.Users.UserInfo.DTOs.Request;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.Features.Users.UserInfo;

public interface IUserInfoService
{
    Task<Result<UserNavbarInfoResponse>> GetNavbarInfoAsync(int userId, CancellationToken ct = default);
    Task<Result<UserProfileInfoResponse>> GetProfileInfoAsync(int userId, CancellationToken ct = default);
    Task<Result<UserProfileInfoResponse>> UpdateProfileAsync(string auth0UserId, UpdateProfileRequest request, CancellationToken ct);

    Task<Result<UserProfileInfoResponse>> UpdateAvatarAsync(string auth0UserId, UpdateAvatarRequest request,
        CancellationToken ct);

}

public sealed class UserInfoService(
    IUserInfoRepository userInfoRepository,
    IImageService imageService,
    IOptions<S3Folders> foldersOptions
) : IUserInfoService
{
    private readonly S3Folders folders = foldersOptions.Value;
    public async Task<Result<UserNavbarInfoResponse>> GetNavbarInfoAsync(int userId, CancellationToken ct = default)
    {
        var user = await userInfoRepository.GetUserWithProfileInfoByUserIdAsync(userId, ct);
        if (user is null || user.ProfileInfo is null)
        {
            return Result<UserNavbarInfoResponse>.NotFound("user_or_profile_info_not_found: User not found");
        }
        var level    = UserLevelCalculator.CalculateLevel(user.Experience);
        var chatIds = user.Chats.Select(c => c.ChatConversationId).ToList();
        var unreadChatThreadsTotal = await userInfoRepository.GetChatUnreadTotalAsync(userId, ct);
        var unreadNotificationTotal = await userInfoRepository.GetNumberOfUnreadNotifications(userId, ct);
        var dto = new UserNavbarInfoResponse(
            user.ID,
            user.ProfileInfo.Nickname,
            user.Email,
            user.Tokens,
            user.EscrowedTokens,
            user.Experience,
            level,
            chatIds,
            user.ProfileInfo.ImageUrl,
            unreadChatThreadsTotal,
            unreadNotificationTotal
        );
        return Result<UserNavbarInfoResponse>.Success(dto);
    }
    public async Task<Result<UserProfileInfoResponse>> GetProfileInfoAsync(int userId, CancellationToken ct = default)
    {
        var user = await userInfoRepository.GetUserWithProfileInfoByUserIdAsync(userId, ct);
        if (user is null || user.ProfileInfo is null)
        {
            return Result<UserProfileInfoResponse>.NotFound(
                "user_or_profile_info_not_found: User or profile info not found");
        }


        var level  = UserLevelCalculator.CalculateLevel(user.Experience);
        var stats = await userInfoRepository.GetUserStatsByUserIdAsync(userId, ct);
        if (stats is null) return Result<UserProfileInfoResponse>.NotFound("user_statistics_not_found");
        var (activeOffersCount, successTradeCount, completedTradeCount, rating) = stats.Value;
            
        var successRate = completedTradeCount == 0 ? 0f : (float)successTradeCount / completedTradeCount;

        var dto = new UserProfileInfoResponse(
            user.ID,
            user.Experience,
            level,
            user.RegistrationDate,
            user.ProfileInfo.Nickname,
            user.ProfileInfo.Description,
            user.ProfileInfo.ImageUrl,
            activeOffersCount,
            successTradeCount,
            rating,
            successRate
        );

        return Result<UserProfileInfoResponse>.Success(dto);
    }

    public async Task<Result<UserProfileInfoResponse>> UpdateProfileAsync(string auth0UserId, UpdateProfileRequest request, CancellationToken ct)
    {
        string trimmedAuth0UserId = auth0UserId.StartsWith("auth0|")
            ? auth0UserId.Substring("auth0|".Length)
            : auth0UserId;
        var user = await userInfoRepository.GetUserWithProfileByAuth0IdAsync(trimmedAuth0UserId, ct);

        if (user is null || user.ProfileInfo is null)
            return Result<UserProfileInfoResponse>.NotFound(
                "user_or_profile_info_not_found: User or profile info not found");
        user.ProfileInfo.Nickname = request.Nickname ?? user.ProfileInfo.Nickname;
        user.ProfileInfo.Description = request.Description ?? user.ProfileInfo.Description;

        await userInfoRepository.UpdateUserWithProfileInfoAsync(user.ProfileInfo, ct);
        
        var level = UserLevelCalculator.CalculateLevel(user.Experience);
        var stats = await userInfoRepository.GetUserStatsByUserIdAsync(user.ID, ct);
        if (stats is null) return Result<UserProfileInfoResponse>.NotFound("user_statistics_not_found");
        var (activeOffersCount, successTradeCount, completedTradeCount, rating) = stats.Value;
            
        var successRate = completedTradeCount == 0 ? 0f : (float)successTradeCount / completedTradeCount;

        var dto = new UserProfileInfoResponse(
            user.ID,
            user.Experience,
            level,
            user.RegistrationDate,
            user.ProfileInfo.Nickname,
            user.ProfileInfo.Description,
            user.ProfileInfo.ImageUrl,
            activeOffersCount,
            successTradeCount,
            rating,
            successRate
        );

        return Result<UserProfileInfoResponse>.Success(dto);
    }

    public async Task<Result<UserProfileInfoResponse>> UpdateAvatarAsync(string auth0UserId, UpdateAvatarRequest request,
        CancellationToken ct)
    {
        string trimmedAuth0UserId = auth0UserId.StartsWith("auth0|")
            ? auth0UserId.Substring("auth0|".Length)
            : auth0UserId;
        var user = await userInfoRepository.GetUserWithProfileByAuth0IdAsync(trimmedAuth0UserId, ct);

        if (user is null || user.ProfileInfo is null)
            return Result<UserProfileInfoResponse>.NotFound(
                "user_or_profile_info_not_found: User or profile info not found");
        string? newImageUrl = null;
        var oldImageUrl = user.ProfileInfo.ImageUrl;
        try
        {
                newImageUrl = await imageService.UploadAsync(
                request.Image,
                folders.Avatars,
                ct);

            user.ProfileInfo.ImageUrl = newImageUrl;

            await userInfoRepository.UpdateUserWithProfileInfoAsync(user.ProfileInfo, ct);

            if (!string.IsNullOrWhiteSpace(oldImageUrl))
            {
                await imageService.DeleteAsync(oldImageUrl, ct);
            }
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(newImageUrl))
            {
                await imageService.DeleteAsync(newImageUrl, ct);
            }

            return Result<UserProfileInfoResponse>.InternalServerError("avatar_upload_failed");
        }
        var level = UserLevelCalculator.CalculateLevel(user.Experience);
        var stats = await userInfoRepository.GetUserStatsByUserIdAsync(user.ID, ct);
        if (stats is null) return Result<UserProfileInfoResponse>.NotFound("user_statistics_not_found");
        var (activeOffersCount, successTradeCount, completedTradeCount, rating) = stats.Value;
            
        var successRate = completedTradeCount == 0 ? 0f : (float)successTradeCount / completedTradeCount;

        var dto = new UserProfileInfoResponse(
            user.ID,
            user.Experience,
            level,
            user.RegistrationDate,
            user.ProfileInfo.Nickname,
            user.ProfileInfo.Description,
            user.ProfileInfo.ImageUrl,
            activeOffersCount,
            successTradeCount,
            rating,
            successRate
        );

        return Result<UserProfileInfoResponse>.Success(dto);
    }
}
