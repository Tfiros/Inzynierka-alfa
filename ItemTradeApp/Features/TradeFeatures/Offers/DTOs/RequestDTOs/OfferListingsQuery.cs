namespace ItemTradeApp.Features.TradeFeatures.Items.DTOs.ResponseDTOs;

public sealed class OfferListingsQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int? GameId { get; set; }
    public int? GenreId { get; set; }

    public string? SearchText { get; set; }
    
    public byte OrderBy { get; set; }
}