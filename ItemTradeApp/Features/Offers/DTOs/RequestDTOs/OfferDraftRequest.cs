using ItemTradeApp.Features.Shared.DTOs;

namespace ItemTradeApp.Features.Offers.DTOs.RequestDTOs;

public sealed class OfferDraftRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public bool IsHighlighted { get; set; }
    public IReadOnlyCollection<OfferItemDTO> OfferedItems { get; set; } = Array.Empty<OfferItemDTO>();
    public IReadOnlyCollection<OfferItemDTO> WantedItems { get; set; } = Array.Empty<OfferItemDTO>();
}