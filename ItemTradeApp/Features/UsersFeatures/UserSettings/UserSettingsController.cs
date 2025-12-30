using System.Security.Claims;
using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.UsersFeature.UserSettings.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ItemTradeApp.Features.UsersFeature.UserSettings;


[ApiController]
[Route("[controller]")]
[Authorize]
public class UserSettingsController(IUserSettingsService service) : ControllerBase
{
    [HttpPut("update-data")]
    public async Task<ActionResult<Result<string>>> UpdateSensitiveData(
        [FromBody] UserDataUpdateRequest? request,
        CancellationToken ct)
    {
        if (request is null)
        {
            var bad = Result<string>.BadRequest("Body is required.");
            return bad.ToActionResult();
        }
        var auth0UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        
        var result = await service.UpdateSensitiveDataAsync(auth0UserId, request, ct);
        return result.ToActionResult();
    }
    
    [Authorize(Policy = "OwnResource")]
    [HttpGet("get-data/{id:int}")]
    public async Task<ActionResult<Result<UserSecurityInfoResponse>>> GetUserSensitiveData(
        int id,
        CancellationToken ct = default)
    {
        var result = await service.GetSecurityProfileInfoAsync(id, ct);
        return result.ToActionResult();
    }
}