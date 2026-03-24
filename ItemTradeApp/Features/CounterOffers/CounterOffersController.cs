using System.Security.Claims;
using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.CounterOffers.DTOs;
using ItemTradeApp.Features.CounterOffers.DTOs.PatchDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.CounterOffers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class CounterOffersController(ICounterOffersService counterOffersService) : ControllerBase
{
    private string GetNormalizedAuth0UserId()
    {
        var user = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(user))
            return string.Empty;

        var pipePosition = user.IndexOf('|');
        return (pipePosition >= 0 && pipePosition < user.Length - 1)
            ? user[(pipePosition + 1)..]
            : user;
    }
    
    [HttpGet("sent")]
    public async Task<ActionResult<Result<IReadOnlyList<CounterOfferListItemDto>>>> GetSent(CancellationToken ct = default)
    {
        var auth0UserId = GetNormalizedAuth0UserId();
        var result = await counterOffersService.GetSentCounterOffers(auth0UserId, ct);
        return result.ToActionResult();
    }
    
    [HttpGet("received")]
    public async Task<ActionResult<Result<IReadOnlyList<CounterOfferListItemDto>>>> GetReceived(CancellationToken ct = default)
    {
        var auth0UserId = GetNormalizedAuth0UserId();
        var result = await counterOffersService.GetReceivedCounterOffers(auth0UserId, ct);
        return result.ToActionResult();
    }
    
    [HttpPost("{offerId:int}/counter")]
    public async Task<ActionResult<Result<CounterOfferDto>>> CreateCounterOffer(
        [FromRoute] int offerId,
        [FromBody] CounterOfferDraftRequest request,
        CancellationToken ct)
    {
        var auth0UserId = GetNormalizedAuth0UserId();
        var result = await counterOffersService.CreateCounterOfferAsync(auth0UserId, offerId, request, ct);
        return result.ToActionResult();
    }
    
    [HttpPatch("{counterOfferId:int}")]
    public async Task<ActionResult<Result<CounterOfferDto>>> UpdateCounterOfferStatus(
        [FromRoute] int counterOfferId,
        [FromBody] UpdateCounterOfferStatusRequest request,
        CancellationToken ct)
    {
        var auth0UserId = GetNormalizedAuth0UserId();
        var result = await counterOffersService.UpdateCounterOfferStatusAsync(
            auth0UserId,
            counterOfferId,
            request.StatusId,
            ct);

        return result.ToActionResult();
    }
    
    [HttpPost("{counterOfferId:int}/accept")]
    public async Task<ActionResult<Result<AcceptCounterOfferResponse>>> AcceptCounterOffer(
        [FromRoute] int counterOfferId,
        CancellationToken ct)
    {
        var auth0UserId = GetNormalizedAuth0UserId();
        var result = await counterOffersService.AcceptCounterOfferAsync(auth0UserId, counterOfferId, ct);
        return result.ToActionResult();
    }
}