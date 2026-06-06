using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Shared;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Shared.Images;
using Microsoft.Extensions.Options;
using ItemTradeApp.Features.Users.UserInfo.DTOs.Request;
using ItemTradeApp.Features.Users.UserInfo.DTOs.Response;

namespace ItemTradeApp.Features.Users.UserInfo;

public interface IUserInfoService
{
    Task<Result<UserNavbarInfoResponse>> GetNavbarInfoAsync(int userId, CancellationToken ct = default);
    Task<Result<UserProfileInfoResponse>> GetProfileInfoAsync(int userId, CancellationToken ct = default);
    Task<Result<UserProfileInfoResponse>> UpdateProfileAsync(string auth0UserId, UpdateProfileRequest request, CancellationToken ct);
    Task<Result<UserProfileInfoResponse>> UpdateAvatarAsync(string auth0UserId, UpdateAvatarRequest request,
        CancellationToken ct);
    Task<Result<PagedResponse<CounterOfferListItemDto>>> GetSentCounterOffers(
        string? auth0UserId,
        CounterOfferListingsQuery query,
        CancellationToken ct = default);
    Task<Result<PagedResponse<CounterOfferListItemDto>>> GetReceivedCounterOffers(
        string? auth0UserId,
        CounterOfferListingsQuery query,
        CancellationToken ct = default);
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
            unreadChatThreadsTotal,
            unreadNotificationTotal,
            user.ProfileInfo.ImageUrl
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
        var trimmedAuth0UserId = Auth0IdHandler.Trim(auth0UserId);
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
        var trimmedAuth0UserId = Auth0IdHandler.Trim(auth0UserId);
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
    public async Task<Result<PagedResponse<CounterOfferListItemDto>>> GetSentCounterOffers(
    string? auth0UserId,
    CounterOfferListingsQuery query,
    CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(auth0UserId))
        return Result<PagedResponse<CounterOfferListItemDto>>.Unauthorized("missing_sub_claim");

    if (query.Page <= 0)
        return Result<PagedResponse<CounterOfferListItemDto>>.BadRequest("invalid_page_number");

    if (query.PageSize <= 0)
        return Result<PagedResponse<CounterOfferListItemDto>>.BadRequest("invalid_page_size");

    query.PageSize = query.PageSize > 100 ? 100 : query.PageSize;

    var trimmedAuth0UserId = Auth0IdHandler.Trim(auth0UserId);
    var user = await userInfoRepository.GetUserWithProfileByAuth0IdAsync(trimmedAuth0UserId, ct);

    if (user is null)
        return Result<PagedResponse<CounterOfferListItemDto>>.NotFound("user_not_found");

    var (items, totalCount) = await userInfoRepository.GetSentCounterOffersAsync(user.ID, query, ct);

    var totalPages = totalCount == 0
        ? 1
        : (int)Math.Ceiling(totalCount / (double)query.PageSize);

    var response = new PagedResponse<CounterOfferListItemDto>
    {
        Page = query.Page,
        PageSize = query.PageSize,
        TotalCount = totalCount,
        TotalPages = totalPages,
        Elements = items
    };

    return Result<PagedResponse<CounterOfferListItemDto>>.Success(response);
}

public async Task<Result<PagedResponse<CounterOfferListItemDto>>> GetReceivedCounterOffers(
    string? auth0UserId,
    CounterOfferListingsQuery query,
    CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(auth0UserId))
        return Result<PagedResponse<CounterOfferListItemDto>>.Unauthorized("missing_sub_claim");

    if (query.Page <= 0)
        return Result<PagedResponse<CounterOfferListItemDto>>.BadRequest("invalid_page_number");

    if (query.PageSize <= 0)
        return Result<PagedResponse<CounterOfferListItemDto>>.BadRequest("invalid_page_size");

    query.PageSize = query.PageSize > 100 ? 100 : query.PageSize;

    var trimmedAuth0UserId = Auth0IdHandler.Trim(auth0UserId);
    var user = await userInfoRepository.GetUserWithProfileByAuth0IdAsync(trimmedAuth0UserId, ct);

    if (user is null)
        return Result<PagedResponse<CounterOfferListItemDto>>.NotFound("user_not_found");

    var (items, totalCount) = await userInfoRepository.GetReceivedCounterOffersAsync(user.ID, query, ct);

    var totalPages = totalCount == 0
        ? 1
        : (int)Math.Ceiling(totalCount / (double)query.PageSize);

    var response = new PagedResponse<CounterOfferListItemDto>
    {
        Page = query.Page,
        PageSize = query.PageSize,
        TotalCount = totalCount,
        TotalPages = totalPages,
        Elements = items
    };

    return Result<PagedResponse<CounterOfferListItemDto>>.Success(response);
}
}
