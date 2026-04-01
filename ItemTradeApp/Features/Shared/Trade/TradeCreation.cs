using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Shared;

public interface ITradeCreation
{
    Task<Trade> ExecuteAsync(CreateTradeDTO context, CancellationToken ct);
}

public sealed class TradeCreator(AppDbContext db) : ITradeCreation
{
    public Task<Trade> ExecuteAsync(CreateTradeDTO context, CancellationToken ct)
    {
        var trade = new Trade
        {
            Offer_ID = context.OfferId,
            Customer_ID = context.CustomerId,
            User_ID = context.UserId,
            TokenCost = context.TokenCost,
            CreationDate = DateTime.UtcNow,
            CompletitionDate = null,
            TradeStatus_ID = (int)TradeStatuses.New,
            MiddlemanUser_ID = context.MiddlemanUserId,
            HasBuyersItems = false,
            HasSellersItems = false
        };

        db.Trades.Add(trade);

        return Task.FromResult(trade);
    }
}