using ItemTradeApp.Persistence;

namespace ItemTradeApp.Features.CounterOffers.DTOs.RequestDTOs;

public class UpdateCounterOfferStatusRequest
{
    public CounterOfferStatuses StatusId { get; set; }
}