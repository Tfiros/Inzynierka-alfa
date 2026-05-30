using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Offers.Repositories;

public interface IGamesRepository
{
    Task<List<Game>> GetAll(CancellationToken ct);
}

public class GamesRepository(AppDbContext db): IGamesRepository
{
    public async Task<List<Game>> GetAll(CancellationToken ct)
        => await db.Games.Where(g => !g.IsDeleted).ToListAsync(ct);
}