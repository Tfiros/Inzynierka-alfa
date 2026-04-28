using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.ItemsManagement.Items;

public interface IItemsRepository
{
    Task<Item?> GetByIdAsync(int id, CancellationToken ct);
    Task AddAsync(Item item, CancellationToken ct);
    Task<(List<Item> Items, int TotalCount)> GetPagedAsync(int gameId,int page, int pageSize, string? searchText, CancellationToken ct);
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class ItemsRepository(AppDbContext db) : IItemsRepository
{
    public async Task<Item?> GetByIdAsync(int id, CancellationToken ct)
        => await db.Items
            .Include(i => i.Game)
            .FirstOrDefaultAsync(i => i.ID == id, ct);

    public Task<bool> ExistsByNameAsync(string name, CancellationToken ct)
            => db.Items.AsNoTracking().AnyAsync(i => i.Name == name, ct);

    public async Task AddAsync(Item item, CancellationToken ct)
        => await db.Items.AddAsync(item, ct).AsTask();
    public async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
    public async Task<(List<Item> Items, int TotalCount)> GetPagedAsync(
        int gameId,
        int page,
        int pageSize,
        string? searchText,
        CancellationToken ct)
    {
        var query = db.Items
            .AsNoTracking()
            .Include(i => i.Game)
            .Where(i => !i.IsDeleted && i.Game_ID == gameId)
            .AsQueryable();

        query = ApplyTextSearch(query, searchText);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(i => i.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
    private static IQueryable<Item> ApplyTextSearch(IQueryable<Item> query, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return query;

        searchText = searchText.Trim();
        return query.Where(g => g.Name.Contains(searchText));
    }

}
