using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.TradeChats.Repositories;

public interface ITradeChatMessageRepository
{
    Task<TradeChatMessage?> GetByIdAsync(int messageId, CancellationToken ct);

    Task<List<TradeChatMessage>> GetMessagesAsync(int tradeChatId, int? beforeMessageId, int pageSize,
        CancellationToken ct);

    Task AddSync(TradeChatMessage message, CancellationToken ct);
}

public class TradeChatMessageRepository(AppDbContext db) : ITradeChatMessageRepository
{
    public Task<TradeChatMessage?> GetByIdAsync(int messageId, CancellationToken ct)
        => db.TradeChatMessages.FirstOrDefaultAsync(tcm => tcm.Id == messageId, ct);

    public Task<List<TradeChatMessage>> GetMessagesAsync(int tradeChatId, int? beforeMessageId, int pageSize,
        CancellationToken ct)
    {
        var q = db.TradeChatMessages
            .AsNoTracking()
            .Include(tcm => tcm.Sender).ThenInclude(s => s.ProfileInfo)
            .Where(tcm => tcm.TradeChatId == tradeChatId);

        if (beforeMessageId is not null)
            q = q.Where(tcm => tcm.Id < beforeMessageId);

        return q.OrderByDescending(tcm => tcm.Id)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task AddSync(TradeChatMessage message, CancellationToken ct)
        => await db.TradeChatMessages.AddAsync(message, ct);
}