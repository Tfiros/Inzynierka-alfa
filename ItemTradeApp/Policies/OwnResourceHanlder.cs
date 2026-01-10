using System.Net;
using System.Security.Claims;
using ItemTradeApp.Features.Users.UserInfo;
using ItemTradeApp.Middlewares.Requirements;
using ItemTradeApp.Persistence.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ItemTradeApp.Middlewares;

public class OwnResourceHanlder : AuthorizationHandler<OwnResourceRequirement>
{
    private readonly IUserInfoRepository _userInfoRepository;

    public OwnResourceHanlder(IUserInfoRepository userInfoRepository)
    {
        _userInfoRepository = userInfoRepository;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnResourceRequirement requirement)
    {
        var auth0UserId =
            context.User.FindFirst("sub")?.Value ??
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(auth0UserId)) return;
        var routeData = context.Resource switch
        {
            Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext mvcCtx 
                => mvcCtx.RouteData,
            HttpContext httpCtx
                => httpCtx.GetRouteData(),
            _ => null
        };

        if (routeData is null)
            return;

        if (!routeData.Values.TryGetValue(requirement.RequirementParameterName, out var rawId))
            return;

        if (!int.TryParse(rawId?.ToString(), out var routeUserId))
            return;
        string trimmedAuth0UserId = auth0UserId.StartsWith("auth0|")
            ? auth0UserId.Substring("auth0|".Length)
            : auth0UserId;

        var user = await _userInfoRepository.GetUserWithProfileByAuth0IdAsync(trimmedAuth0UserId, CancellationToken.None);
        if (user is null)
            return;

        if (user.ID == routeUserId)
        {
            context.Succeed(requirement);
        }
    }
}