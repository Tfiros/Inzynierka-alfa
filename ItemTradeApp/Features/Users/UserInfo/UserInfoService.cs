using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Persistence;
using ItemTradeApp.Features.Users.UserInfo.DTOs.Response;
using ItemTradeApp.Features.Users.UserInfo.DTOs.Request;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Users.UserInfo;

public interface IUserInfoService
{
    Task<Result<UserNavbarInfoResponse>> GetNavbarInfoAsync(int userId, CancellationToken ct = default);
    Task<Result<UserProfileInfoResponse>> GetProfileInfoAsync(int userId, CancellationToken ct = default);
    Task<Result<UserProfileInfoResponse>> UpdateProfileAsync(string auth0UserId, UpdateProfileRequest request, CancellationToken ct);

}

public sealed class UserInfoService(IUserInfoRepository userInfoRepository) : IUserInfoService
{
    public async Task<Result<UserNavbarInfoResponse>> GetNavbarInfoAsync(int userId, CancellationToken ct = default)
    {
        var user = await userInfoRepository.GetUserWithProfileInfoByUserIdAsync(userId, ct);
        if (user is null)
        {
            return Result<UserNavbarInfoResponse>.NotFound("user_not_found: User not found");
        }
        var level    = UserLevelCalculator.CalculateLevel(user.Experience);

        var dto = new UserNavbarInfoResponse(
            user.ID,
            user.ProfileInfo.Nickname,
            user.Email,
            user.Tokens,
            user.Experience,
            level
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

        var dto = new UserProfileInfoResponse(
            user.ID,
            user.Experience,
            level,
            user.RegistrationDate,
            user.ProfileInfo.Nickname,
            user.ProfileInfo.Description
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

        var dto = new UserProfileInfoResponse(
            user.ID,
            user.Experience,
            level,
            user.RegistrationDate,
            user.ProfileInfo.Nickname,
            user.ProfileInfo.Description
        );

        return Result<UserProfileInfoResponse>.Success(dto);
    }
}
