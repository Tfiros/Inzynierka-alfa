using ItemTradeApp.Features.CounterOffers.DTOs;
using ItemTradeApp.Features.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.CounterOffers;

[ApiController]
[Route("[controller]")]
public class CounterOffersController (ICounterOffersService counterOffersService): ControllerBase
{
    [HttpGet ("{offerId:int}/counter")]
    [Authorize]
    public async Task<IActionResult> GetForCounter(int offerId, CancellationToken ct)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return Unauthorized();
        
        var result = await counterOffersService.GetOfferInfo(offerId, auth0UserId, ct);
        
        if (!result.IsSuccess)
            return BadRequest(result);

        return Ok(result);
    }
    
    [HttpGet("sent/{userId:int}")]
    [Authorize]
    public async Task<ActionResult<Result<IReadOnlyList<CounterOfferListItemDto>>>> GetSent(
        [FromRoute] int userId,
        CancellationToken ct = default)
    {
        var result = await counterOffersService.GetSentCounterOffers(userId, ct);

        if (!result.IsSuccess)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpGet("received/{userId:int}")]
    [Authorize]
    public async Task<ActionResult<Result<IReadOnlyList<CounterOfferListItemDto>>>> GetReceived(
        [FromRoute] int userId,
        CancellationToken ct = default)
    {
        var result = await counterOffersService.GetRecivedCounterOffers(userId, ct);

        if (!result.IsSuccess)
            return BadRequest(result);

        return Ok(result);
    }
}