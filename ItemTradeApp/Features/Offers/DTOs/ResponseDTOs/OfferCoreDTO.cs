namespace ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;

public sealed record OfferCoreDTO(int OfferId,DateOnly ExpDate, DateTime CreationDate, int TokenCost, int OfferStatusId);