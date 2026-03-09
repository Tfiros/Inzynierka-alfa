using System.Security.Claims;
using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.CounterOffers.DTOs;
using ItemTradeApp.Features.CounterOffers.DTOs.PatchDTOs;
using ItemTradeApp.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.CounterOffers;

[ApiController]
[Route("[controller]")]
public class CounterOffersController (ICounterOffersService counterOffersService, AppDbContext db): ControllerBase
{
    private string? GetNormalizedAuth0UserId()
    {
        var rawInput = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(rawInput)) return null;

        var pipePosition = rawInput.IndexOf('|');
        return (pipePosition >= 0 && pipePosition < rawInput.Length - 1) ? rawInput[(pipePosition + 1)..] : rawInput;
    }
    
    [HttpGet("{offerId:int}/info")]
    [Authorize]
    public async Task<ActionResult<Result<OfferInformationDTO>>> GetOfferInfo(int offerId, CancellationToken ct)
    {
        var auth0UserId = GetNormalizedAuth0UserId();
        if (auth0UserId is null)
            return Unauthorized();

        var result = await counterOffersService.GetOfferInfoAsync(auth0UserId, offerId, ct);
        return result.ToActionResult();
    }
    
    [HttpGet("sent")]
    [Authorize]
    public async Task<ActionResult<Result<IReadOnlyList<CounterOfferListItemDto>>>> GetSent(CancellationToken ct = default)
    {
        var auth0UserId = GetNormalizedAuth0UserId();
        if (auth0UserId is null)
            return Unauthorized();

        var userId = await db.Users
            .AsNoTracking()
            .Where(u => u.Auth0UserID == auth0UserId && !u.IsDeleted)
            .Select(u => (int?)u.ID)
            .FirstOrDefaultAsync(ct);

        if (userId is null)
            return Unauthorized();

        var result = await counterOffersService.GetSentCounterOffers(userId.Value, ct);
        return result.ToActionResult();
    }

    [HttpGet("received")]
    [Authorize]
    public async Task<ActionResult<Result<IReadOnlyList<CounterOfferListItemDto>>>> GetReceived(CancellationToken ct = default)
    {
        var auth0UserId = GetNormalizedAuth0UserId();
        if (auth0UserId is null)
            return Unauthorized();

        var userId = await db.Users
            .AsNoTracking()
            .Where(u => u.Auth0UserID == auth0UserId && !u.IsDeleted)
            .Select(u => (int?)u.ID)
            .FirstOrDefaultAsync(ct);

        if (userId is null)
            return Unauthorized();

        var result = await counterOffersService.GetRecivedCounterOffers(userId.Value, ct);
        return result.ToActionResult();
    }
    

    [HttpPost("{offerId:int}/counter")]
    [Authorize]
    public async Task<ActionResult<Result<CounterOfferDto>>> CreateCounterOffer(
        [FromRoute] int offerId,
        [FromBody] CounterOfferDraftRequest request,
        CancellationToken ct)
    {
        var auth0UserId = GetNormalizedAuth0UserId();
        if (auth0UserId is null)
            return Unauthorized();

        var result = await counterOffersService.CreateCounterOfferAsync(auth0UserId, offerId, request, ct);

        return result.ToActionResult();
    }

    [HttpPatch("{counterOfferId:int}")]
    [Authorize]
    public async Task<ActionResult<Result<CounterOfferDto>>> UpdateCounterOfferStatus(
        [FromRoute] int counterOfferId,
        [FromBody] UpdateCounterOfferStatusRequest request,
        CancellationToken ct)
    {
        var auth0UserId = GetNormalizedAuth0UserId();
        if (auth0UserId is null)
            return Unauthorized();

        var result = await counterOffersService.UpdateCounterOfferStatusAsync(
            auth0UserId,
            counterOfferId,
            request.StatusId,
            ct);

        return result.ToActionResult();
    }
    [HttpPost("{counterOfferId:int}/accept")]
    [Authorize]
    public async Task<ActionResult<Result<AcceptCounterOfferResponse>>> AcceptCounterOffer(
        [FromRoute] int counterOfferId,
        CancellationToken ct)
    {
        var auth0UserId = GetNormalizedAuth0UserId();
        if (auth0UserId is null)
            return Unauthorized();

        var result = await counterOffersService.AcceptCounterOfferAsync(auth0UserId, counterOfferId, ct);
        return result.ToActionResult();
    }
}