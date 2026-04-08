using ItemTradeApp.Features.Shared;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Trades;

public sealed class TradeCreator(AppDbContext db) : ITradeCreation
{
    public async Task<Trade> ExecuteAsync(CreateTradeDTO context, CancellationToken ct)
    {
        var trade = new Trade
        {
            Offer_ID = context.OfferId,
            Customer_ID = context.BuyerId,
            User_ID = context.SellerId,
            TokenCost = context.TokenCost,
            CreationDate = DateTime.UtcNow,
            CompletitionDate = null,
            TradeStatus_ID = (int)TradeStatuses.New,
            MiddlemanUser_ID = null,
            HasBuyersItems = false,
            HasSellersItems = false
        };

        await db.Trades.AddAsync(trade, ct);
        return trade;
    }
}