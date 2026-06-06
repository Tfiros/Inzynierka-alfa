namespace ItemTradeApp.Features.Users.UserInfo.DTOs.Request;

public sealed class CounterOfferListingsQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public CounterOffersOrderByEnum OrderBy { get; set; } =
        CounterOffersOrderByEnum.CreationDateDesc;
}

public enum CounterOffersOrderByEnum
{
    CreationDateAsc = 1,
    CreationDateDesc = 2,
    TokensAsc = 3,
    TokensDesc = 4
}