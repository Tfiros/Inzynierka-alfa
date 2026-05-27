using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Offers.Repositories;

public interface IItemsRepository
{
    Task<List<Item>> GetByName(string searchText, CancellationToken ct);
    Task<List<Item>> GetByNameAndGameId(string searchText, int gameId, CancellationToken ct);
}

public sealed class ItemsRepository(AppDbContext db) : IItemsRepository
{
    public async Task<List<Item>> GetByName(string searchText, CancellationToken ct)
    {
        searchText = searchText.Trim();
        if (searchText.Length < 3)
        {
            return new List<Item>();
        }
        var escaped = Escape(searchText);


        var query = db.Items.AsNoTracking().Include(i => i.Game).Where(i => !i.IsDeleted && EF.Functions.ILike(i.Name, $"%{escaped}%", "!"));


        return await query
            .OrderByDescending(i => i.EstimatedTokenValue)
            .Take(5)
            .ToListAsync(ct);
    }

    public async Task<List<Item>> GetByNameAndGameId(string searchText, int gameId, CancellationToken ct)
    {
        searchText = searchText.Trim();
        if (searchText.Length <= 3)
        {
            return new List<Item>();
        }

        var escaped = Escape(searchText);
        var query = db.Items.AsNoTracking().Include(i => i.Game).Where(i => !i.IsDeleted && i.Game_ID == gameId && EF.Functions.ILike(i.Name, $"%{escaped}%","!"));

        return await query
            .OrderByDescending(i => i.EstimatedTokenValue)
            .Take(5)
            .ToListAsync(ct);
    }

    private static string Escape(string input, char escapeChar = '!')
    {
        return input.Replace(escapeChar.ToString(), new string(escapeChar, 2))
            .Replace("%", $"{escapeChar}%")
            .Replace("_", $"{escapeChar}_");
    }
}