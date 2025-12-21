namespace ItemTradeApp.Features.TradeFeatures.Offers.DTOs.ResponseDTOs;

public class OfferListingsPagedReponse 
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasNextPage { get; set; }
    public List<OfferListingDTO> Items { get; set; } = [];
}