using System.Security.Claims;
using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Trades.DTOs;
using ItemTradeApp.Features.Trades.DTOs.Request;
using ItemTradeApp.Features.Trades.DTOs.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.Trades;

[ApiController]
[Route("[controller]")]
public sealed class TradesController(ITradesService tradesService) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Result<int>>> Create([FromBody] CreateTradeRequest? request, CancellationToken ct)
    {
        var auth0UserId =
            User.FindFirstValue("sub") ??
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        var res = await tradesService.CreateAsync(request, auth0UserId, ct);
        return res.ToActionResult();
    }

    [HttpPost("assign-middleman")]
    [Authorize(Roles = "Middleman")]
    public async Task<ActionResult<Result<string>>> AssignMiddleman([FromBody] AssignMiddlemanRequest? request, CancellationToken ct)
    {
        var auth0UserId =
            User.FindFirstValue("sub") ??
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        var res = await tradesService.AssignMiddlemanAsync(request, auth0UserId, ct);
        return res.ToActionResult();
    }
    [HttpPut("update-trade/{tradeId}")]
    [Authorize(Roles = "Middleman")]
    public async Task<ActionResult<Result<string>>> UpdateByMiddleman(
        [FromRoute] int tradeId,
        [FromBody] UpdateTradeRequest? request,
        CancellationToken ct)
    {
        var auth0UserId =
            User.FindFirstValue("sub") ??
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        var res = await tradesService.UpdateTradeByMiddlemanAsync(tradeId, request, auth0UserId, ct);
        return res.ToActionResult();
    }
    [HttpGet("created")]
    [Authorize]
    public async Task<ActionResult<Result<PagedResponse<TradeListItemDTO>>>> GetAvailableNew(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] TradesQuery? q = null,
        CancellationToken ct = default)
    {
        var isMiddleman = User.IsInRole("Middleman");
        var auth0UserId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var res = await tradesService.GetAvailableNewAsync(page, pageSize,isMiddleman,auth0UserId, q, ct);
        return res.ToActionResult();
    }

    [HttpGet("in-realization")]
    [Authorize]
    public async Task<ActionResult<Result<PagedResponse<TradeListItemDTO>>>> GetMyInRealization(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] TradesQuery? q = null,
        CancellationToken ct = default)
    {
        var auth0UserId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var res = await tradesService.GetMyInRealizationAsync(auth0UserId, page, pageSize, q, ct);
        return res.ToActionResult();
    }

    [HttpGet("completed")]
    [Authorize]
    public async Task<ActionResult<Result<PagedResponse<TradeListItemDTO>>>> GetMyCompleted(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] TradesQuery? q = null,
        CancellationToken ct = default)
    {
        var auth0UserId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var res = await tradesService.GetMyCompletedAsync(auth0UserId, page, pageSize, q, ct);
        return res.ToActionResult();
    }
    [HttpGet("middleman/stats")]
    [Authorize(Roles = "Middleman")]
    public async Task<ActionResult<Result<MiddlemanTradesStatsResponse>>> GetMiddlemanStats(CancellationToken ct = default)
    {
        var auth0UserId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var res = await tradesService.GetMiddlemanStatsAsync(auth0UserId, ct);
        return res.ToActionResult();
    }
    [HttpGet("middleman/{tradeId:int}/details")]
    [Authorize(Roles = "Middleman")]
    public async Task<ActionResult<Result<TradeDetailsResponse>>> GetTradeDetails(
        [FromRoute] int tradeId,
        CancellationToken ct = default)
    {
        var auth0UserId = User.FindFirstValue("sub")
                          ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        var res = await tradesService.GetTradeDetailsAsync(auth0UserId, tradeId, ct);
        return res.ToActionResult();
    }
    
    [HttpGet("middleman/failed-with-return")]
    [Authorize(Roles = "Middleman")]
    public async Task<ActionResult<Result<PagedResponse<TradeListItemDTO>>>> GetFailedTradeWithReturn(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] TradesQuery? q = null,
        CancellationToken ct = default)
    {
        var auth0UserId = User.FindFirstValue("sub")
                          ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        var res = await tradesService.GetMyFailedWithItemsToReturnAsync(auth0UserId, page, pageSize, q, ct);
        return res.ToActionResult();
    }
    
    [HttpPut("middleman/{tradeId:int}/set-failed")]
    [Authorize(Roles = "Middleman")]
    public async Task<ActionResult<Result<string>>> SetTradeAsFailed(
        [FromRoute] int tradeId,
        CancellationToken ct = default)
    {
        var auth0UserId = User.FindFirstValue("sub")
                          ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        var res = await tradesService.SetTradeAsFailed(tradeId, auth0UserId , ct);
        return res.ToActionResult();
    }
    
    [HttpPut("middleman/{tradeId:int}/set-realised")]
    [Authorize(Roles = "Middleman")]
    public async Task<ActionResult<Result<string>>> SetTradeAsRealised(
        [FromRoute] int tradeId,
        CancellationToken ct = default)
    {
        var auth0UserId = User.FindFirstValue("sub")
                          ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        var res = await tradesService.SetTradeAsRealised(tradeId, auth0UserId , ct);
        return res.ToActionResult();
    }


}