using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.UsersFeature.UserInfo.DTOs.Request;
using ItemTradeApp.Features.UsersFeature.UserInfo.DTOs.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.UsersFeature.UserInfo;

[ApiController]
[Route("[controller]")]
[Authorize]
public class UserInfoController(IUserInfoService service) : ControllerBase
{
    [HttpGet("profileInfo/{id:int}")]
    public async Task<ActionResult<Result<UserProfileInfoResponse>>> GetProfileInfo(
        int id,
        CancellationToken ct = default)
    {
        var result = await service.GetProfileInfoAsync(id, ct);
        return result.ToActionResult();
    }
    [Authorize(Policy = "OwnResource")]
    [HttpGet("navbarInfo/{id:int}")]
    public async Task<ActionResult<Result<UserNavbarInfoResponse>>> GetUserNavbarInfo(
        int id,
        CancellationToken ct = default)
    {
        var result = await service.GetNavbarInfoAsync(id, ct);
        return result.ToActionResult();
    }
    [Authorize(Policy = "OwnResource")]
    [HttpPut("profile")]
    public async Task<ActionResult<Result<UserProfileInfoResponse>>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken ct)
    {
        if (request is null)
        {
            var bad = Result<UserProfileInfoResponse>.BadRequest("Body is required");
            return bad.ToActionResult();
        }

        var auth0UserId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(auth0UserId))
        {
            var unauthorized = Result<UserProfileInfoResponse>.Unauthorized("Missing sub claim in JWT.");
            return unauthorized.ToActionResult();
        }

        var result = await service.UpdateProfileAsync(auth0UserId, request, ct);
        return result.ToActionResult();
    }
}