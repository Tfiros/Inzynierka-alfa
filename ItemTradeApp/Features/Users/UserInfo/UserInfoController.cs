using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Shared;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;
using ItemTradeApp.features.Users.UserInfo.DTOs.Request;
using ItemTradeApp.Features.Users.UserInfo.DTOs.Request;
using ItemTradeApp.features.Users.UserInfo.DTOs.Response;
using ItemTradeApp.Features.Users.UserInfo.DTOs.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.Users.UserInfo;

[ApiController]
[Route("[controller]")]
[Authorize]
public class UserInfoController(IUserInfoService userInfoService, IUserInfoOfferService userInfoOfferService) : ControllerBase
{
    [HttpGet("profileInfo/{id:int}")]
    public async Task<ActionResult<Result<UserProfileInfoResponse>>> GetProfileInfo(
        int id,
        CancellationToken ct = default)
    {
        var result = await userInfoService.GetProfileInfoAsync(id, ct);
        return result.ToActionResult();
    }
    [Authorize(Policy = "OwnResource")]
    [HttpGet("navbarInfo/{id:int}")]
    public async Task<ActionResult<Result<UserNavbarInfoResponse>>> GetUserNavbarInfo(
        int id,
        CancellationToken ct = default)
    {
        var result = await userInfoService.GetNavbarInfoAsync(id, ct);
        return result.ToActionResult();
    }
    [Authorize(Policy = "OwnResource")]
    [HttpPut("profileInfo/{id:int}")]
    public async Task<ActionResult<Result<UserProfileInfoResponse>>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken ct)
    {
        if (request is null)
        {
            var bad = Result<UserProfileInfoResponse>.BadRequest("Body is required");
            return bad.ToActionResult();
        }

        var auth0UserId = Auth0IdHandler.GetUserId(User);
        if (string.IsNullOrWhiteSpace(auth0UserId))
        {
            var unauthorized = Result<UserProfileInfoResponse>.Unauthorized("Missing sub claim in JWT.");
            return unauthorized.ToActionResult();
        }

        var result = await userInfoService.UpdateProfileAsync(auth0UserId, request, ct);
        return result.ToActionResult();
    }
    
    [HttpGet("profileInfo/{id:int}/offers/active")]
    public async Task<ActionResult<Result<PagedResponse<OfferListingDTO>>>> GetUserActiveOffers(
        int id, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10, 
        CancellationToken ct = default)
    {
        var result = await userInfoOfferService.GetPagedActiveAsync(id,page,pageSize, ct);
        return result.ToActionResult();
    }
    [HttpGet("profileInfo/{id:int}/offers/history")]
    public async Task<ActionResult<Result<PagedResponse<OfferListingDTO>>>> GetUserHistoryOffers(
        int id, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10, 
        CancellationToken ct = default)
    {
        var result = await userInfoOfferService.GetPagedHistoryAsync(id, page,pageSize, ct);
        return result.ToActionResult();
    }
    
    [Authorize(Policy = "OwnResource")]
    [HttpPut("profileInfo/{id:int}/avatar")]
    public async Task<ActionResult<Result<UserProfileInfoResponse>>> UpdateAvatar(
        [FromForm] UpdateAvatarRequest request,
        CancellationToken ct = default)
    {
        if (request.Image is null)
        {
            var bad = Result<UserProfileInfoResponse>.BadRequest("Image is required");
            return bad.ToActionResult();
        }

        var auth0UserId = Auth0IdHandler.GetUserId(User);
        if (string.IsNullOrWhiteSpace(auth0UserId))
        {
            var unauthorized = Result<UserProfileInfoResponse>.Unauthorized("Missing sub claim in JWT.");
            return unauthorized.ToActionResult();
        }

        var result = await userInfoService.UpdateAvatarAsync(auth0UserId, request, ct);
        return result.ToActionResult();
    }
    [HttpGet("counteroffers/sent")]
    public async Task<ActionResult<Result<PagedResponse<CounterOfferListItemDto>>>> GetSent(
        [FromQuery] CounterOfferListingsQuery query,
        CancellationToken ct)
    {
        var auth0UserId = Auth0IdHandler.GetUserId(User);
        var result = await userInfoService.GetSentCounterOffers(auth0UserId, query, ct);
        return result.ToActionResult();
    }

    [HttpGet("counteroffers/received")]
    public async Task<ActionResult<Result<PagedResponse<CounterOfferListItemDto>>>> GetReceived(
        [FromQuery] CounterOfferListingsQuery query,
        CancellationToken ct)
    {
        var auth0UserId = Auth0IdHandler.GetUserId(User);
        var result = await userInfoService.GetReceivedCounterOffers(auth0UserId, query, ct);
        return result.ToActionResult();
    }
}