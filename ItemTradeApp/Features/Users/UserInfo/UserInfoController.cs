using System.Security.Claims;
using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;
using ItemTradeApp.Features.Users.UserInfo.DTOs.Request;
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

        var auth0UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(auth0UserId))
        {
            var unauthorized = Result<UserProfileInfoResponse>.Unauthorized("Missing sub claim in JWT.");
            return unauthorized.ToActionResult();
        }

        var result = await userInfoService.UpdateProfileAsync(auth0UserId, request, ct);
        return result.ToActionResult();
    }
    
    [HttpGet("userInfo/{id:int}/offers/active")]
    public async Task<ActionResult<Result<PagedResponse<OfferListingDTO>>>> GetUserActiveOffers(
        int id, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10, 
        CancellationToken ct = default)
    {
        var result = await userInfoOfferService.GetPagedActiveAsync(page,pageSize,id, ct);
        return result.ToActionResult();
    }
    [HttpGet("userInfo/{id:int}/offers/history")]
    public async Task<ActionResult<Result<PagedResponse<OfferListingDTO>>>> GetUserHistoryOffers(
        int id, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10, 
        CancellationToken ct = default)
    {
        var result = await userInfoOfferService.GetPagedHistoryAsync(page,pageSize,id, ct);
        return result.ToActionResult();
    }
}