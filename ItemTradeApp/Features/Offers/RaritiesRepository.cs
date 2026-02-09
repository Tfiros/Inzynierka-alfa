using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Offers;

public interface IRaritiesRepository
{
    Task<List<ItemRarity>> GetByGameId(int gameId, CancellationToken ct);
}

public class RaritiesRepository(AppDbContext db) : IRaritiesRepository
{
    public async Task<List<ItemRarity>> GetByGameId(int gameId, CancellationToken ct)
        => await db.ItemRarities
            .Where(r => r.GameId == gameId && !r.IsDeleted)
            .ToListAsync(ct);
}
