using System.Security.Claims;
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
        var auth0UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        string trimmedAuth0UserId = auth0UserId.StartsWith("auth0|")
            ? auth0UserId.Substring("auth0|".Length)
            : auth0UserId;
        var result = await offerService.CreateOfferAsync(trimmedAuth0UserId, request, ct);
        return result.ToActionResult();

    }

    [HttpDelete("{offerId:int}")]
    [Authorize]
    public async Task<ActionResult<Result<string>>> CancelOffer(
        [FromRoute] int offerId, CancellationToken ct = default
    )
    {
        var auth0UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        string trimmedAuth0UserId = auth0UserId.StartsWith("auth0|")
            ? auth0UserId.Substring("auth0|".Length)
            : auth0UserId;
        var result = await offerService.CancelOfferAsync(trimmedAuth0UserId, offerId, ct);
        return result.ToActionResult();
    }

    [HttpPut("{offerId:int}")]
    [Authorize]
    public async Task<ActionResult<Result<OfferDetailsDTO>>> UpdateOffer(
        [FromRoute] int offerId,
        [FromBody] OfferUpdateDraftRequest request,
        CancellationToken ct = default
    )
    {
        var auth0UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        string trimmedAuth0UserId = auth0UserId.StartsWith("auth0|")
            ? auth0UserId.Substring("auth0|".Length)
            : auth0UserId;
        var result = await offerService.UpdateOfferAsync(trimmedAuth0UserId, offerId, request, ct);
        return result.ToActionResult();
    }

    [HttpPost("quote")]
    [Authorize]
    public async Task<ActionResult<Result<OfferQuoteResponse>>> Quote([FromBody] OfferDraftRequest req,
        CancellationToken ct = default)
    {
        var result = await offerService.GetQuoteAsync(req, ct);
        return result.ToActionResult();
    }
    
    [HttpGet("items/suggestions")]
    public async Task<ActionResult<Result<List<ItemDTO>>>> ItemSuggestion(string searchText,
        CancellationToken ct = default)
    {
        var result = await offerService.GetItemsByName(searchText, ct);
        return result.ToActionResult();
    }
    [HttpGet("items")]
    public async Task<ActionResult<Result<List<ItemDTO>>>> Items(string searchText, int gameId,
        CancellationToken ct = default)
    {
        var result = await offerService.GetItemsByNameAndGameId(searchText,gameId, ct);
        return result.ToActionResult();
    }
    
    [HttpGet("games")]
    public async Task<ActionResult<Result<List<GameDTO>>>> Games(CancellationToken ct = default)
    {
        var result = await offerService.GetAllGames(ct);
        return result.ToActionResult();
    }

    [HttpGet("genres")]
    public async Task<ActionResult<Result<List<GenreDTO>>>> Genres(CancellationToken ct = default)
    {
        var result = await offerService.GetAllGenres(ct);
        return result.ToActionResult();
    }

    [HttpGet("rarities")]
    public async Task<ActionResult<Result<List<RarityDTO>>>> Rarities([FromQuery] int gameId, CancellationToken ct = default)
    {
        var result = await offerService.GetRaritiesByGameId(gameId, ct);
        return result.ToActionResult();
    }
    
    [HttpPost("{offerId:int}/quote")]
    [Authorize]
    public async Task<ActionResult<Result<OfferUpdateQuoteResponse>>> QuoteUpdate(
        [FromRoute] int offerId,[FromBody] OfferUpdateDraftRequest request, CancellationToken ct = default)
    {
        var auth0UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        string trimmedAuth0UserId = auth0UserId.StartsWith("auth0|")
            ? auth0UserId.Substring("auth0|".Length)
            : auth0UserId;

        var result = await offerService.GetUpdateQuoteAsync(trimmedAuth0UserId,offerId, request, ct);
        return result.ToActionResult();

    }
    
    

}