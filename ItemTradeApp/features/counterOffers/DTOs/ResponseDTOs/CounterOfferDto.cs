namespace ItemTradeApp.Features.CounterOffers.DTOs.ResponseDTOs;

public sealed record CounterOfferDto
{
    public int Id { get; init; }
    public int OfferId { get; init; }
    public int UserId { get; init; }
    public DateTime CreationDate { get; init; }
    public int CounterOfferStatusId { get; init; }
    public int TokensOffered { get; init; }

    public IReadOnlyCollection<CounterOfferItemDto> Items { get; init; } = Array.Empty<CounterOfferItemDto>();
}