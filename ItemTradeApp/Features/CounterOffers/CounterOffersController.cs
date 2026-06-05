using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.CounterOffers.DTOs;
using ItemTradeApp.Features.CounterOffers.DTOs.RequestDTOs;
using ItemTradeApp.Features.CounterOffers.DTOs.ResponseDTOs;
using ItemTradeApp.Features.Shared;
using ItemTradeApp.Features.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.CounterOffers;

[ApiController]
[Route("[controller]")]
[Authorize]
public sealed class CounterOffersController(ICounterOffersService counterOffersService) : ControllerBase
{
    
    [HttpGet("sent")]
    public async Task<ActionResult<Result<PagedResponse<CounterOfferListItemDto>>>> GetSent(
        [FromQuery] CounterOfferListingsQuery query,
        CancellationToken ct)
    {
        var auth0UserId = Auth0IdHandler.GetUserId(User);
        var result = await counterOffersService.GetSentCounterOffers(auth0UserId, query, ct);
        return result.ToActionResult();
    }

    [HttpGet("received")]
    public async Task<ActionResult<Result<PagedResponse<CounterOfferListItemDto>>>> GetReceived(
        [FromQuery] CounterOfferListingsQuery query,
        CancellationToken ct)
    {
        var auth0UserId = Auth0IdHandler.GetUserId(User);
        var result = await counterOffersService.GetReceivedCounterOffers(auth0UserId, query, ct);
        return result.ToActionResult();
    }
    
    [HttpPost("{offerId:int}/counter")]
    public async Task<ActionResult<Result<CounterOfferDto>>> CreateCounterOffer(
        [FromRoute] int offerId,
        [FromBody] CounterOfferDraftRequest request,
        CancellationToken ct)
    {
        var auth0UserId = Auth0IdHandler.GetUserId(User);
        var result = await counterOffersService.CreateCounterOfferAsync(auth0UserId, offerId, request, ct);
        return result.ToActionResult();
    }
    
    [HttpPatch("{counterOfferId:int}")]
    public async Task<ActionResult<Result<CounterOfferDto>>> UpdateCounterOfferStatus(
        [FromRoute] int counterOfferId,
        [FromBody] UpdateCounterOfferStatusRequest request,
        CancellationToken ct)
    {
        var auth0UserId = Auth0IdHandler.GetUserId(User);
        var result = await counterOffersService.UpdateCounterOfferStatusAsync(
            auth0UserId,
            counterOfferId,
            (int)request.StatusId,
            ct);

        return result.ToActionResult();
    }
    
    [HttpPost("{counterOfferId:int}/accept")]
    public async Task<ActionResult<Result<AcceptCounterOfferResponse>>> AcceptCounterOffer(
        [FromRoute] int counterOfferId,
        CancellationToken ct)
    {
        var auth0UserId = Auth0IdHandler.GetUserId(User);
        var result = await counterOffersService.AcceptCounterOfferAsync(auth0UserId, counterOfferId, ct);
        return result.ToActionResult();
    }
    
    [HttpPost("{offerId:int}/quote")]
    public async Task<ActionResult<Result<CounterOfferCostDto>>> QuoteCounterOffer(
        [FromRoute] int offerId,
        [FromBody] CounterOfferDraftRequest request,
        CancellationToken ct)
    {
        var auth0UserId = Auth0IdHandler.GetUserId(User);

        var result = await counterOffersService.QuoteCounterOfferAsync(
            auth0UserId,
            offerId,
            request,
            ct);

        return result.ToActionResult();
    }
    
    [HttpGet("offer/{offerId:int}")]
    [Authorize]
    public async Task<ActionResult<Result<List<CounterOfferListItemDto>>>> GetForOffer(
        [FromRoute] int offerId,
        CancellationToken ct)
    {
        var res = await counterOffersService.GetCounterOffersForOfferAsync(
            Auth0IdHandler.GetUserId(User),
            offerId,
            ct
        );

        return res.ToActionResult();
    }

    [HttpPost("{counterOfferId:int}/cancel")]
    public async Task<ActionResult<Result<CounterOfferDto>>> CancelCounterOffer([FromRoute] int counterOfferId,
        CancellationToken ct)
    {
        var auth0UserId = Auth0IdHandler.GetUserId(User);
        var result = await counterOffersService.CancelCounterOfferAsync(auth0UserId, counterOfferId, ct);
        return result.ToActionResult();
    }
}