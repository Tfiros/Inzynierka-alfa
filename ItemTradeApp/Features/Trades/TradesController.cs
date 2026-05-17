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
    private string? GetAuth0UserId()
        => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpPost("assign-middleman")]
    [Authorize(Roles = "Middleman")]
    public async Task<ActionResult<Result<string>>> AssignMiddleman(
        [FromBody] AssignMiddlemanRequest? request,
        CancellationToken ct)
    {
        var res = await tradesService.AssignMiddlemanAsync(request, GetAuth0UserId(), ct);
        return res.ToActionResult();
    }

    [HttpPut("update-trade/{tradeId:int}")]
    [Authorize(Roles = "Middleman")]
    public async Task<ActionResult<Result<string>>> UpdateByMiddleman(
        [FromRoute] int tradeId,
        [FromBody] UpdateTradeRequest? request,
        CancellationToken ct)
    {
        var res = await tradesService.UpdateTradeByMiddlemanAsync(tradeId, request, GetAuth0UserId(), ct);
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
        var isMiddleman = User.IsInRole("Middleman") || User.IsInRole("Admin");
        var res = await tradesService.GetAvailableNewAsync(page, pageSize, q, GetAuth0UserId(), isMiddleman, ct);
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
        var res = await tradesService.GetMyInRealizationAsync(page, pageSize, q, GetAuth0UserId(), ct);
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
        var res = await tradesService.GetMyCompletedAsync(page, pageSize, q, GetAuth0UserId(), ct);
        return res.ToActionResult();
    }

    [HttpGet("failed-with-return")]
    [Authorize(Roles = "Middleman")]
    public async Task<ActionResult<Result<PagedResponse<TradeListItemDTO>>>> GetMyFailedWithItemsToReturn(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] TradesQuery? q = null,
        CancellationToken ct = default)
    {
        var res = await tradesService.GetMyFailedWithItemsToReturnAsync(page, pageSize, q, GetAuth0UserId(), ct);
        return res.ToActionResult();
    }

    [HttpGet("stats")]
    [Authorize]
    public async Task<ActionResult<Result<UserTradeStatsResponse>>> GetStats(CancellationToken ct = default)
    {
        var auth0UserId = GetAuth0UserId();
        var isMiddleman = User.IsInRole("Middleman") || User.IsInRole("Admin");

        var res = await tradesService.GetStatsAsync(auth0UserId, isMiddleman, ct);
        return res.ToActionResult();
    }


    [HttpGet("middleman/{tradeId:int}/details")]
    [Authorize(Roles = "Middleman")]
    public async Task<ActionResult<Result<TradeDetailsResponse>>> GetTradeDetails(
        [FromRoute] int tradeId,
        CancellationToken ct = default)
    {
        var res = await tradesService.GetTradeDetailsAsync(tradeId, GetAuth0UserId(), ct);
        return res.ToActionResult();
    }

    [HttpPut("middleman/{tradeId:int}/set-as-failed")]
    [Authorize(Roles = "Middleman")]
    public async Task<ActionResult<Result<string>>> SetTradeAsFailed(
        [FromRoute] int tradeId,
        CancellationToken ct = default)
    {
        var res = await tradesService.SetTradeAsFailedAsync(tradeId, GetAuth0UserId(), ct);
        return res.ToActionResult();
    }

    [HttpPut("middleman/{tradeId:int}/set-realised")]
    [Authorize(Roles = "Middleman")]
    public async Task<ActionResult<Result<string>>> SetTradeAsRealised(
        [FromRoute] int tradeId,
        [FromBody] CompleteAndMarkTradeRequest request,
        CancellationToken ct = default)
    {
        var res = await tradesService.SetTradeAsRealisedAsync(tradeId, GetAuth0UserId(), request, ct);
        return res.ToActionResult();
    }


    [HttpGet("{tradeId:int}")]
    [Authorize]
    public async Task<ActionResult<Result<TradeListItemDTO>>> GetById([FromRoute] int tradeId, CancellationToken ct)
    {
        var isMiddlemanView = User.IsInRole("Middleman");
        var res = await tradesService.GetByIdAsync(tradeId, GetAuth0UserId(), isMiddlemanView, ct);
        return res.ToActionResult();
    }
    
    [HttpPost("{tradeId:int}/photos")]
    [Authorize(Roles = "Middleman")]
    public async Task<ActionResult<Result<string>>> UploadTradePhoto(
        [FromRoute] int tradeId,
        [FromForm] UploadTradeImageRequest request,
        CancellationToken ct = default)
    {
        var res = await tradesService.UploadTradeImageAsync(
            tradeId,
            request,
            GetAuth0UserId(),
            ct);

        return res.ToActionResult();
    }
}
