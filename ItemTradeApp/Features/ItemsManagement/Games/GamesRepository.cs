using ItemTradeApp.Features.Shared;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.ItemsManagement.Games;

public interface IGamesRepository
{
    Task<Game?> GetByIdAsync(int id, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task<(List<Game> Items, int TotalCount)> GetPagedAsync(int genreId, int page, int pageSize, string? searchText, CancellationToken ct);
    Task<Game?> GetByNameAsync(string name,  CancellationToken ct);
    Task<Game?> GetByIdWithNoTrackAsync(int id, CancellationToken ct);
    Task<Game> CreateWithRaritiesAsync(
        Game game,
        IReadOnlyCollection<ItemRarity> rarities,
        CancellationToken ct);
    Task SoftDeleteCascadeAsync(int gameId, CancellationToken ct);

    Task<List<Game>> GetGamesForDropdown(string? searchText, CancellationToken ct);

}


public sealed class GamesRepository(AppDbContext db) : IGamesRepository
{
    public async Task<Game?> GetByIdAsync(int id, CancellationToken ct)
        => await db.Games.Include(g => g.Genre).FirstOrDefaultAsync(g => g.ID == id, ct);

    public async Task<Game?> GetByIdWithNoTrackAsync(int id, CancellationToken ct) =>
        await db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.ID == id, ct);

    public async Task<List<Game>> GetGamesForDropdown(string? searchText, CancellationToken ct)
    {
        IQueryable<Game> query = db.Games
            .Where(g => !g.IsDeleted);

        query = ApplyTextSearch(query, searchText);

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

        query = ApplyTextSearch(query, searchText);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .AsNoTracking()
            .Include(g => g.Genre)
            .OrderBy(g => g.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Game> CreateWithRaritiesAsync(
        Game game,
        IReadOnlyCollection<ItemRarity> rarities,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        db.Games.Add(game);
        await db.SaveChangesAsync(ct);

        foreach (var rarity in rarities)
            rarity.GameId = game.ID;

        db.ItemRarities.AddRange(rarities);
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        return game;
    }

    public async Task SoftDeleteCascadeAsync(int gameId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var game = await db.Games
            .Include(g => g.Items)
            .Include(g => g.ItemRarities)
            .FirstOrDefaultAsync(g => g.ID == gameId, ct);

        if (game is null || game.IsDeleted)
            return;

        game.IsDeleted = true;

        foreach (var item in game.Items)
            if (!item.IsDeleted)
                item.IsDeleted = true;

        foreach (var rarity in game.ItemRarities)
            if (!rarity.IsDeleted)
                rarity.IsDeleted = true;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<Game?> GetByNameAsync(string name, CancellationToken ct)
        =>  await db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Name == name, ct);
    

    public async Task SaveChangesAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);

    private static IQueryable<Game> ApplyTextSearch(IQueryable<Game> query, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return query;

        searchText = searchText.Trim();
        return query.Where(g => g.Name.Contains(searchText));
    }


}
