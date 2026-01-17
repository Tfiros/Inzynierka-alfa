using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Offers.DTOs.RequestDTOs;
using ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.Offers;

[ApiController]
[Route("[controller]")]
public class OffersController(IOffersService offerService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<Result<PagedResponse<OfferListingDTO>>>> GetOffers(
        [FromQuery] OfferListingsQuery query, CancellationToken ct = default
        )
    {
        var result = await offerService.GetOffersAsync(query, ct);
        return result.ToActionResult();
    }
    
    [HttpGet("{offerId:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<Result<OfferDetailsDTO>>> GetOfferDetails(
        [FromRoute] int offerId, CancellationToken ct = default
    )
    {
        var result = await offerService.GetOfferByIdAsync(offerId, ct);
        return result.ToActionResult();
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Result<OfferDetailsDTO>>> CreateOffer(
        [FromBody] OfferDraftRequest request, CancellationToken ct = default)
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        var result = await offerService.CreateOfferAsync(auth0UserId ?? string.Empty, request, ct);
        return result.ToActionResult();

    }

    [HttpDelete("{offerId:int}")]
    [Authorize]
    public async Task<ActionResult<Result<string>>> CancelOffer(
        [FromRoute] int offerId, CancellationToken ct = default
    )
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        var result = await offerService.CancelOfferAsync(auth0UserId ?? string.Empty, offerId, ct);
        return result.ToActionResult();
    }

    [HttpPut("{offerId:int}")]
    [Authorize]
    public async Task<ActionResult<Result<OfferDetailsDTO>>> UpdateOffer(
        [FromRoute] int offerId,
        [FromBody] OfferDraftRequest request,
        CancellationToken ct = default
    )
    {
        var auth0UserId = User.FindFirst("sub")?.Value;
        var result = await offerService.UpdateOfferAsync(auth0UserId ?? string.Empty, offerId, request, ct);
        return result.ToActionResult();
    }

    [HttpPost("quote")]
    public async Task<ActionResult<Result<OfferQuoteResponse>>> Quote([FromBody] OfferDraftRequest req,
        CancellationToken ct = default)
    {
        var result = await offerService.GetQuoteAsync(req, ct);
        return result.ToActionResult();
    }
    
    [HttpGet("items")]
    public async Task<ActionResult<Result<List<ItemDTO>>>> ItemSuggestion(string searchText,
        CancellationToken ct = default)
    {
        var result = await offerService.GetByName(searchText, ct);
        return result.ToActionResult();
    }

}