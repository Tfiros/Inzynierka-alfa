using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Trades.Repositories;

public interface ITradeRepository
{
    IQueryable<Trade> QueryNoTracking();
    Task<Trade?> GetTradeWithOfferAndUsersDetailsByIdAsync(int tradeId, CancellationToken ct);
    Task AddAsync(Trade trade, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task<Trade?> GetByIdAsync(int tradeId, CancellationToken ct);
    Task<Trade?> GetByIdWithUrlsAsync(int tradeId, CancellationToken ct);
    Task<Trade?> GetTradeDetailsAsync(int tradeId, CancellationToken ct);
    Task<bool> ExistsActiveForOfferAsync(int offerId, CancellationToken ct);

    Task<(int All, int Completed, int MyActive, int Available)> GetMiddlemanStatsAsync(int middlemanUserId,
        CancellationToken ct);

    Task<(int All, int Completed, int MyActive, int Created)>
        GetUserStatsAsync(int postingUserId, CancellationToken ct);

    Task<Trade?> GetTradeWithOfferByIdAsync(int tradeId, CancellationToken ct);
}

public sealed class TradeRepository(AppDbContext db) : ITradeRepository
{
    public IQueryable<Trade> QueryNoTracking() => db.Trades.AsNoTracking();

    public async Task<Trade?> GetTradeWithOfferAndUsersDetailsByIdAsync(int tradeId, CancellationToken ct)
        => await db.Trades
            .Where(t => t.ID == tradeId)
            .Include(t => t.Offer)
            .Include(t => t.Customer)
            .ThenInclude(u => u.ProfileInfo)
            .Include(t => t.PostingUser)
            .ThenInclude(u => u.ProfileInfo)
            .FirstOrDefaultAsync(ct);

    public async Task<Trade?> GetTradeWithOfferByIdAsync(int tradeId, CancellationToken ct) =>
        await db.Trades.Where(t => t.ID == tradeId)
            .Include(t => t.Offer)
            .FirstOrDefaultAsync(ct);
    public async Task AddAsync(Trade trade, CancellationToken ct)
        => await db.Trades.AddAsync(trade, ct).AsTask();

    public async Task SaveChangesAsync(CancellationToken ct)
        => await db.SaveChangesAsync(ct);

    public async Task<Trade?> GetByIdAsync(int tradeId, CancellationToken ct)
        => await db.Trades.FirstOrDefaultAsync(t => t.ID == tradeId, ct);

    public async Task<Trade?> GetByIdWithUrlsAsync(int tradeId, CancellationToken ct)
        => await db.Trades.Include(t => t.Urls).FirstOrDefaultAsync(t => t.ID == tradeId, ct);

    public async Task<bool> ExistsActiveForOfferAsync(int offerId, CancellationToken ct)
        => await db.Trades.AnyAsync(t =>
            t.Offer_ID == offerId &&
            (t.TradeStatus_ID == (int)TradeStatuses.New ||
             t.TradeStatus_ID == (int)TradeStatuses.InRealization), ct);

    public async Task<Trade?> GetTradeDetailsAsync(int tradeId, CancellationToken ct)
        => await db.Trades
            .AsNoTracking()
            .Include(t => t.Customer).ThenInclude(u => u.ProfileInfo)
            .Include(t => t.PostingUser).ThenInclude(u => u.ProfileInfo)
            .Include(t => t.Urls)
            .FirstOrDefaultAsync(t => t.ID == tradeId, ct);

    public async Task<(int All, int Completed, int MyActive, int Available)> GetMiddlemanStatsAsync(
        int middlemanUserId,
        CancellationToken ct)
    {
        var raw = await db.Trades
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                All = g.Count(),
                Completed = g.Count(t => t.TradeStatus_ID == (int)TradeStatuses.SuccesfulRealization && t.MiddlemanUser_ID == middlemanUserId),
                MyActive = g.Count(t =>
                    t.TradeStatus_ID == (int)TradeStatuses.InRealization &&
                    t.MiddlemanUser_ID == middlemanUserId),
                Available = g.Count(t =>
                    t.TradeStatus_ID == (int)TradeStatuses.New &&
                    t.MiddlemanUser_ID == null)
            })
            .SingleOrDefaultAsync(ct);

        return raw is null
            ? (0, 0, 0, 0)
            : (raw.All, raw.Completed, raw.MyActive, raw.Available);
    }
    public async Task<(int All, int Completed, int MyActive, int Created)> GetUserStatsAsync(
        int postingUserId,
        CancellationToken ct)
    {
        var raw = await db.Trades
            .AsNoTracking()
            .Where(t => t.User_ID == postingUserId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                All = g.Count(t => t.User_ID == postingUserId),
                Completed = g.Count(t => t.TradeStatus_ID == (int)TradeStatuses.SuccesfulRealization && t.User_ID == postingUserId),
                MyActive = g.Count(t => t.TradeStatus_ID == (int)TradeStatuses.InRealization && t.User_ID == postingUserId),
                Created = g.Count(t => t.TradeStatus_ID == (int)TradeStatuses.New && t.User_ID == postingUserId)
            })
            .SingleOrDefaultAsync(ct);

        return raw is null
            ? (0, 0, 0, 0)
            : (raw.All, raw.Completed, raw.MyActive, raw.Created);
    }

}
