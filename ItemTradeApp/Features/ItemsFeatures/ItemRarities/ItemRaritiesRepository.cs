using ItemTradeApp.Features.Shared;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.ItemsFeatures.ItemRarities;

public interface IItemRarityRepository
{
    Task<bool> GameExistsAsync(int gameId, CancellationToken ct);
    Task<bool> ExistsActiveByNameAsync(int gameId, string rarityName, CancellationToken ct);
    Task SoftDeleteCascadeAsync(int id, CancellationToken ct);
    Task<ItemRarity?> GetByIdAsync(int id, CancellationToken ct);
    Task<ItemRarity?> GetByIdWithNoTrackAsync(int id, CancellationToken ct);
    Task<List<ItemRarity>> SearchForDropdownAsync(int gameId, string? searchText, CancellationToken ct);

    Task<(List<ItemRarity> Items, int TotalCount)> GetPagedAsync(
        int gameId,
        int page,
        int pageSize,
        string? searchText,
        CancellationToken ct);

    Task AddAsync(ItemRarity entity, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed class ItemRarityRepository(AppDbContext db) : IItemRarityRepository
{
    public Task<bool> GameExistsAsync(int gameId, CancellationToken ct) =>
        db.Games.AnyAsync(g => g.ID == gameId && !g.IsDeleted, ct);

    public async Task<ItemRarity?> GetByIdWithNoTrackAsync(int id, CancellationToken ct) =>
        await db.ItemRarities.AsNoTracking().FirstOrDefaultAsync(r => r.ID == id, ct);

    public Task<bool> ExistsActiveByNameAsync(int gameId, string rarityName, CancellationToken ct) =>
        db.ItemRarities.AnyAsync(r =>
            r.GameId == gameId &&
            !r.IsDeleted &&
            r.RarityName == rarityName, ct);

    public Task<ItemRarity?> GetByIdAsync(int id, CancellationToken ct) =>
        db.ItemRarities.FirstOrDefaultAsync(r => r.ID == id, ct);

    public async Task<List<ItemRarity>> SearchForDropdownAsync(int gameId, string? searchText, CancellationToken ct)
    {
        var q = db.ItemRarities.AsNoTracking()
            .Where(r => r.GameId == gameId && !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var s = searchText.Trim();
            q = q.Where(r => EF.Functions.ILike(r.RarityName, $"%{s}%"));
        }

        return await q
            .OrderBy(r => r.RarityName)
            .Take(Consts.DROPDOWN_LIMIT)
            .ToListAsync(ct);
    }

    public async Task<(List<ItemRarity> Items, int TotalCount)> GetPagedAsync(
        int gameId,
        int page,
        int pageSize,
        string? searchText,
        CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 50) pageSize = 50;

        var q = db.ItemRarities.AsNoTracking()
            .Where(r => r.GameId == gameId && !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var s = searchText.Trim();
            q = q.Where(r => EF.Functions.ILike(r.RarityName, $"%{s}%"));
        }

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderBy(r => r.RarityName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
    public async Task SoftDeleteCascadeAsync(int id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var rarity = await db.ItemRarities.FirstOrDefaultAsync(r => r.ID == id, ct);
        if (rarity is null || rarity.IsDeleted)
            return;

        rarity.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        var items = await db.Items
            .Where(i => i.ItemRarityId == id && !i.IsDeleted)
            .ToListAsync(ct);

        foreach (var item in items)
            item.IsDeleted = true;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public Task AddAsync(ItemRarity entity, CancellationToken ct) =>
        db.ItemRarities.AddAsync(entity, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);
}
