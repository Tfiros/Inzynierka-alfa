

using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.TradeChats.Repositories;

public interface ITradeChatRepository
{
    Task<TradeChat?> GetByIdAsync(int tradeChatId, CancellationToken ct);
    Task<List<TradeChat>> GetByTradeIdAsync(int tradeId, CancellationToken ct);

    Task CreateChatsAsync(IEnumerable<TradeChat> chats, CancellationToken ct);
    Task CloseChatsForTradeAsync(int tradeId, DateTime closedAtUtc, CancellationToken ct);
}

public class TradeChatRepository(AppDbContext db) : ITradeChatRepository
{
    public Task<TradeChat?> GetByIdAsync(int tradeChatId, CancellationToken ct)
        => db.TradeChats
            .Include(x => x.Participant).ThenInclude(p => p.ProfileInfo)
            .Include(x => x.Middleman).ThenInclude(m => m.ProfileInfo)
            .FirstOrDefaultAsync(x => x.Id == tradeChatId, ct);

    public Task<List<TradeChat>> GetByTradeIdAsync(int tradeId, CancellationToken ct)
        => db.TradeChats
            .Include(x => x.Participant).ThenInclude(p => p.ProfileInfo)
            .Include(x => x.Middleman).ThenInclude(m => m.ProfileInfo)
            .Where(tc => tc.TradeId == tradeId)
            .OrderBy(x => x.Id).ToListAsync(ct);

    public Task CreateChatsAsync(IEnumerable<TradeChat> chats, CancellationToken ct)
        => db.TradeChats.AddRangeAsync(chats, ct);

    public Task CloseChatsForTradeAsync(int tradeId, DateTime closedAtUtc, CancellationToken ct)
        => db.TradeChats
            .Where(tc => tc.TradeId == tradeId && tc.ClosedAt == null)
            .ExecuteUpdateAsync(tc => tc.SetProperty(u => u.ClosedAt, _ => closedAtUtc), ct);

}