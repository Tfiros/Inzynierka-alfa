using ItemTradeApp.ExceptionsHandling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.UsersFeature.UserInfo;
[ApiController]
[Route("[controller]")]
[Authorize]
public class UserInfoController(IUserInfoService service) : Controller
{
    
    [HttpGet("/profileInfo/{id:int}")]
    public async Task<IActionResult> GetProfileInfo(int id, CancellationToken ct = default)
    {
        var result = await service.GetProfileInfoAsync(id, ct);

        return result.Matching(
            ok  => Ok(ok),
            err => StatusCode(err.StatusCode, err)
        );
    }

    [HttpGet("/userInfo/{id:int}")]
    public async Task<IActionResult> GetUserInfo(int id, CancellationToken ct= default)
    {
        var result = await service.GetNavbarInfoAsync(id, ct);

        return result.Matching(
            ok  => Ok(ok),
            err => StatusCode(err.StatusCode, err)
        );
    }
}
