using ItemTradeApp.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.CounterOffers;

public interface IItemsRepository
{
    Task<bool> AllItemsExistAsync(int[] itemIds, CancellationToken ct);
}

public sealed class ItemsRepository(AppDbContext db):IItemsRepository
{
    public async Task<bool> AllItemsExistAsync(int[] itemIds, CancellationToken ct)
    {
        var uniqueItemIds = itemIds.Distinct().ToArray();

        var existingCount = await db.Items.CountAsync(
            i => uniqueItemIds.Contains(i.ID) && !i.IsDeleted,
            ct
        );

        return existingCount == uniqueItemIds.Length;
    }
}