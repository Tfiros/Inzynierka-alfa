using ItemTradeApp.Features.Shared.DTOs;

namespace ItemTradeApp.Features.Offers.DTOs.RequestDTOs;

public sealed class OfferUpdateDraftRequest
{
    public int DurationDays { get; set; }
    public bool IsHighlighted { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TokensOffered { get; set; }
    public int TokensWanted { get; set; }
    public IReadOnlyCollection<OfferItemDTO> OfferedItems { get; set; } = Array.Empty<OfferItemDTO>();
    public IReadOnlyCollection<OfferItemDTO> WantedItems { get; set; } = Array.Empty<OfferItemDTO>();
}