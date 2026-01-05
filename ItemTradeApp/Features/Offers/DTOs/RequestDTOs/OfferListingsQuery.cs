namespace ItemTradeApp.Features.Offers.DTOs.RequestDTOs;

public sealed class OfferListingsQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int? GameId { get; set; }
    public int? GenreId { get; set; }
    public string? SearchText { get; set; } = null;
    public OffersOrderByEnum OrderBy { get; set; } = OffersOrderByEnum.CreationDateDesc;
}