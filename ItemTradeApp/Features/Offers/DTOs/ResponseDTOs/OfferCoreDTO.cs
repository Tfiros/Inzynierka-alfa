namespace ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;

public sealed record OfferCoreDTO(int OfferId, string Title, string Description, DateOnly ExpDate, DateTime CreationDate, int TokenCost, int OfferStatusId);