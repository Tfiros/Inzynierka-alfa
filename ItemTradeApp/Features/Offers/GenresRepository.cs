using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Offers;

public interface IGenresRepository
{
    Task<List<Genre>> GetAll(CancellationToken ct);
}

public class GenresRepository(AppDbContext db) : IGenresRepository
{
    public async Task<List<Genre>> GetAll(CancellationToken ct)
        => await db.Genres.Where(g => !g.IsDeleted).ToListAsync(ct);
}
