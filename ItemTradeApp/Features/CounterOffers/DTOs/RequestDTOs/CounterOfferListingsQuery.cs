namespace ItemTradeApp.Features.CounterOffers.DTOs.RequestDTOs;

public sealed class CounterOfferListingsQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public CounterOffersOrderByEnum OrderBy { get; set; } =
        CounterOffersOrderByEnum.CreationDateDesc;
}