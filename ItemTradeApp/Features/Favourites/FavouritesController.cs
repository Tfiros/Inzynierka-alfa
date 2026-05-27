using System.Security.Claims;
using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Shared;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.Favourites;

[ApiController]
[Route("[controller]")]
[Authorize]
public class FavouritesController(IFavouritesService favouritesService) : ControllerBase
{

    [HttpGet]
    public async Task<ActionResult<Result<PagedResponse<OfferListingDTO>>>> GetFavourites([FromQuery] int page = 1,
        [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var user = Auth0IdHandler.GetUserId(User);
        var result = await favouritesService.GetFavouritesAsync(user, page, pageSize, ct);
        return result.ToActionResult();
    }
    
    [HttpGet("ids")]
    public async Task<ActionResult<Result<List<int>>>> GetFavouriteIds(CancellationToken ct = default)
    {
        var user = Auth0IdHandler.GetUserId(User);
        var result = await favouritesService.GetFavouriteIdsAsync(user, ct);
        return result.ToActionResult();
    }
    
    [HttpPost("{offerId:int}")]
    public async Task<ActionResult<Result<bool>>> AddFavourite([FromRoute] int offerId, CancellationToken ct = default)
    {
        var user = Auth0IdHandler.GetUserId(User);
        var result = await favouritesService.AddFavourite(user, offerId, ct);
        return result.ToActionResult();
    }
    
    [HttpDelete("{offerId:int}")]
    public async Task<ActionResult<Result<bool>>> RemoveFavourite([FromRoute] int offerId, CancellationToken ct = default)
    {
        var user = Auth0IdHandler.GetUserId(User);
        var result = await favouritesService.RemoveFavourite(user, offerId, ct);
        return result.ToActionResult();
    }
    
}