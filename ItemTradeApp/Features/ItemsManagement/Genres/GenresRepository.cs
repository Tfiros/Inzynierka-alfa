using ItemTradeApp.Features.Shared;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.ItemsManagement.Genres;

public interface IGenresRepository
{
    Task<Genre?> GetByIdAsync(int id, CancellationToken ct);
    Task<Genre?> GetByIdWithNoTrackAsync(int id, CancellationToken ct);
    Task<Genre?> GetByNameAsync(string name, CancellationToken ct);
    Task AddAsync(Genre genre, CancellationToken ct);
    Task<List<Genre>> GetGenresForDropdownAsync(string? searchText, CancellationToken ct);
    Task<(List<Genre> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? searchText, CancellationToken ct);
    Task SoftDeleteCascadeAsync(int genreId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class GenresRepository(AppDbContext db) : IGenresRepository
{
    public async Task<List<Genre>> GetGenresForDropdownAsync(string? searchText, CancellationToken ct)
    {
        var query = db.Set<Genre>()
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .AsQueryable();

        query = ApplyTextSearch(query, searchText);

        return await query
            .OrderBy(x => x.Name)
            .Take(Consts.DROPDOWN_LIMIT)
            .ToListAsync(ct);
    }
    
    public async Task<Genre?> GetByIdWithNoTrackAsync(int id, CancellationToken ct) =>
        await db.Genres.AsNoTracking().FirstOrDefaultAsync(g => g.ID == id, ct);
    public async Task<Genre?> GetByIdAsync(int id, CancellationToken ct)
        => await db.Genres.FirstOrDefaultAsync(x => x.ID == id, ct);
    public async Task<Genre?> GetByNameAsync(string name, CancellationToken ct)
        => await db.Genres.AsNoTracking().FirstOrDefaultAsync(x => x.Name == name && !x.IsDeleted, ct);
    public async Task AddAsync(Genre genre, CancellationToken ct)
        => await db.Genres.AddAsync(genre, ct).AsTask();
    public async Task<(List<Genre> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? searchText, CancellationToken ct)
    {
        var query = db.Genres
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .AsQueryable();

        query = ApplyTextSearch(query, searchText);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
    public async Task SoftDeleteCascadeAsync(int genreId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var genre = await db.Genres
            .Include(g => g.Games)
                .ThenInclude(game => game.Items)
            .Include(g => g.Games)
                .ThenInclude(game => game.ItemRarities)
            .FirstOrDefaultAsync(g => g.ID == genreId, ct);

        if (genre is null || genre.IsDeleted)
            return;

        genre.IsDeleted = true;
        foreach (var game in genre.Games)
        {
            if (game.IsDeleted) continue;
            game.IsDeleted = true;
            foreach (var item in game.Items)
            {
                if (!item.IsDeleted)
                    item.IsDeleted = true;
            }
            foreach (var rarity in game.ItemRarities)
            {
                if (!rarity.IsDeleted)
                    rarity.IsDeleted = true;
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    private static IQueryable<Genre> ApplyTextSearch(IQueryable<Genre> query, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return query;

        searchText = searchText.Trim();
        return query.Where(g => g.Name.Contains(searchText));
    }

}
