using ItemTradeApp.Features.Shared;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.ItemsFeatures.Genres;

public interface IGenresRepository
{
    Task<Genre?> GetByIdAsync(int id, CancellationToken ct);
    Task<bool> ExistsActiveByNameAsync(string name, CancellationToken ct);
    Task AddAsync(Genre genre, CancellationToken ct);
    Task<List<Genre>> GetGenresForDropdownAsync(string? searchText, CancellationToken ct);
    Task<(List<Genre> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? searchText, CancellationToken ct);
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class GenresRepository(AppDbContext db) : IGenresRepository
{
    public async Task<List<Genre>> GetGenresForDropdownAsync(string? searchText, CancellationToken ct)
    {
        var query = db.Set<Genre>()
            .Where(x => !x.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            searchText = searchText.Trim();
            query = query.Where(x => x.Name.Contains(searchText));
        }

        return await query
            .OrderBy(x => x.Name)
            .Take(Consts.DROPDOWN_LIMIT)
            .ToListAsync(ct);
    }

    public Task<bool> ExistsByNameAsync(string name, CancellationToken ct)
        => db.Genres.AnyAsync(x => x.Name == name, ct);


    public Task<Genre?> GetByIdAsync(int id, CancellationToken ct)
        => db.Genres.FirstOrDefaultAsync(x => x.ID == id, ct);
    public async Task<bool> ExistsActiveByNameAsync(string name, CancellationToken ct)
        => await db.Genres.AnyAsync(x => x.Name == name && !x.IsDeleted, ct);
    public async Task AddAsync(Genre genre, CancellationToken ct)
        => await db.Genres.AddAsync(genre, ct).AsTask();
    public async Task<(List<Genre> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? searchText, CancellationToken ct)
    {
        var query = db.Genres
            .Where(x => !x.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            searchText = searchText.Trim();
            query = query.Where(x => x.Name.Contains(searchText));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
