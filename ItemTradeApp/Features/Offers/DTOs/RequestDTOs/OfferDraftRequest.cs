namespace ItemTradeApp.Features.Offers.DTOs.RequestDTOs;

public sealed class OfferDraftRequest
{
    public int DurationDays { get; set; }
    public bool IsHighlighted { get; set; }
    public IReadOnlyCollection<OfferItemDTO> OfferedItems { get; set; } = Array.Empty<OfferItemDTO>();
    public IReadOnlyCollection<OfferItemDTO> WantedItems { get; set; } = Array.Empty<OfferItemDTO>();
}