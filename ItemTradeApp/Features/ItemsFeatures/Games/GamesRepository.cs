using ItemTradeApp.Features.Shared;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.ItemsFeatures.Games;

public interface IGamesRepository
{
    Task<Game?> GetByIdAsync(int id, CancellationToken ct);
    Task AddAsync(Game game, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task<(List<Game> Items, int TotalCount)> GetPagedAsync(int genreId, int page, int pageSize, string? searchText, CancellationToken ct);
    Task<bool> ExistsByNameAsync(string name,  CancellationToken ct);

    Task<List<Game>> GetGamesForDropdown(string? searchText, CancellationToken ct);

}


public sealed class GamesRepository(AppDbContext db) : IGamesRepository
{
    public async Task<Game?> GetByIdAsync(int id, CancellationToken ct)
        => await db.Games.Include(g => g.Genre).FirstOrDefaultAsync(g => g.ID == id, ct);

    public async Task<List<Game>> GetGamesForDropdown(string? searchText, CancellationToken ct)
    {
        IQueryable<Game> query = db.Games
            .Where(g => !g.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            searchText = searchText.Trim();
            query = query.Where(g => g.Name.Contains(searchText));
        }

        return await query
            .OrderBy(g => g.Name)
            .Take(Consts.DROPDOWN_LIMIT)
            .ToListAsync(ct);
    }

    
    public async Task<(List<Game> Items, int TotalCount)> GetPagedAsync(
        int genreId,
        int page,
        int pageSize,
        string? searchText,
        CancellationToken ct)
    {
        var query = db.Games
            .Where(g => !g.IsDeleted)
            .AsQueryable();

        if (genreId > 0)
            query = query.Where(g => g.Genre_ID == genreId);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            searchText = searchText.Trim();
            query = query.Where(g => g.Name.Contains(searchText));
        }

        var totalCount = await query.CountAsync(ct);
        var test = await db.Games
            .Where(g => !g.IsDeleted && g.Genre_ID == genreId)
            .AsNoTracking()
            .Take(5)
            .Select(g => new { g.ID, g.Name, g.Genre_ID, g.IsDeleted })
            .ToListAsync(ct);

        var items = await query
            .Include(g => g.Genre)
            .OrderBy(g => g.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Game game, CancellationToken ct)
        => await db.Games.AddAsync(game, ct);

    public Task<bool> ExistsByNameAsync(string name, CancellationToken ct)
        => db.Games.AnyAsync(g => g.Name == name, ct);

    public async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
