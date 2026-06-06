using ItemTradeApp.Features.Shared.TradeCreation;
using ItemTradeApp.Features.Shared.TradeCreation.DTOs;
using ItemTradeApp.Features.Trades.Repositories;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Trades;

public sealed class TradeCreator(ITradeRepository tradeRepository) : ITradeCreation
{
    public async Task<Trade> ExecuteAsync(CreateTradeContext context, CancellationToken ct)
    {
        var trade = new Trade
        {
            Offer_ID = context.OfferId,
            AcceptedCounterOffer_ID = context.CounterOfferId,
            Customer_ID = context.BuyerId,
            Seller_ID = context.SellerId,
            CreationDate = DateTime.UtcNow,
            CompletitionDate = null,
            TradeStatus_ID = (int)TradeStatuses.New,
            MiddlemanUser_ID = null,
            HasBuyersItems = false,
            HasSellersItems = false
        };

        await tradeRepository.AddAsync(trade, ct);
        return trade;
    }
}