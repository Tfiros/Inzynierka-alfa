using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.UsersFeature.UserManagement.DTOs;
using ItemTradeApp.Features.UsersFeature.UserManagement.DTOs.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.UsersFeature.UserManagement;
[ApiController]
[Route("[controller]")]
[Authorize(Roles = "Admin")]
public class UserManagementController(IUserManagementService userManagementService)
{
    [HttpPatch]
    public async Task<ActionResult<Result<string>>> UpdateUser(
        [FromBody] UpdateUserRequest? request,
        CancellationToken ct)
    {
        if (request is null)
        {
            var bad = Result<string>.BadRequest("Body is required.");
            return bad.ToActionResult();
        }

        var result = await userManagementService.UpdateUserAsync( request, ct);
        return result.ToActionResult();
    }
    [HttpDelete]
    public async Task<ActionResult<Result<string>>> DeleteUser(
        [FromBody] DeleteUserRequest request,
        CancellationToken ct = default)
    {
        var result = await userManagementService.DeleteUserAsync(request.AuthZeroUserId, ct);
        return result.ToActionResult();
    }
    [HttpGet]
    public async Task<ActionResult<Result<UserListPagedResponse>>> GetUsers(
        [FromQuery] UserListQuery query,
        CancellationToken ct)
    {
        var result = await userManagementService.GetUsersAsync(query, ct);
        return result.ToActionResult();
    }
}