using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.TradeFeatures.Items.DTOs.ResponseDTOs;
using ItemTradeApp.Features.TradeFeatures.Offers;
using ItemTradeApp.Features.TradeFeatures.Offers.DTOs.ResponseDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.TradeFeatures.Items;

[ApiController]
[Route("[controller]")]
public class OfferController(IItemService itemService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<Result<OfferListingsPagedReponse>>> GetOffers(
        [FromQuery] OfferListingsQuery query, CancellationToken ct = default
        )
    {
        var result = await itemService.GetOffersAsync(query, ct);
        return result.ToActionResult();
    }
}