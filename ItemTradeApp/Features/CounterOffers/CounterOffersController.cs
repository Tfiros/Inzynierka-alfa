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
        var result = await counterOffersService.GetOfferInfo(offerId, auth0UserId, ct);
        
        if (!result.IsSuccess)
            return BadRequest(result);

        return Ok(result);
    }
}