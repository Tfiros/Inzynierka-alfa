using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Persistence;
using ItemTradeApp.Features.UsersFeature.UserInfo.DTOs.Response;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.UsersFeature.UserInfo;

public interface IUserInfoService
{
    Task<Result<UserNavbarInfoResponse>> GetNavbarInfoAsync(int userId, CancellationToken ct = default);
    Task<Result<UserProfileInfoResponse>> GetProfileInfoAsync(int userId, CancellationToken ct = default);
}

public sealed class UserInfoService(IUserInfoRepository userInfoRepository) : IUserInfoService
{
    public async Task<Result<UserNavbarInfoResponse>> GetNavbarInfoAsync(int userId, CancellationToken ct = default)
    {
        var user = await userInfoRepository.GetUserAsync(userId, ct);
        if (user is null)
        {
            return Result<UserNavbarInfoResponse>.Fail(
                new AppError(404, "User not found", "user_not_found"));
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
        return Result<UserNavbarInfoResponse>.Ok(dto);
    }

    public async Task<Result<UserProfileInfoResponse>> GetProfileInfoAsync(int userId, CancellationToken ct = default)
    {
        var user = await userInfoRepository.GetUserAsync(userId, ct);
        if (user is null)
        {
            return Result<UserProfileInfoResponse>.Fail(
                new AppError(404, "User not found", "user_not_found"));
        }


        var level  = UserLevelCalculator.CalculateLevel(user.Experience);

        var dto = new UserProfileInfoResponse(
            user.ID,
            user.Email,
            user.DateOfBirth,
            user.Tokens,
            user.Experience,
            level,
            user.RegistrationDate,
            user.ProfileInfo.Nickname,
            user.ProfileInfo.Description
        );

        return Result<UserProfileInfoResponse>.Ok(dto);
    }
}
