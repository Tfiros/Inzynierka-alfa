using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Shared.Emails;

public interface IEmailOutboxRepository
{
    Task SaveSentAsync(Persistence.Models.Emails row, CancellationToken ct);
}

public sealed class EmailsRepository(AppDbContext db) : IEmailOutboxRepository
{
    public async Task SaveSentAsync(Persistence.Models.Emails row, CancellationToken ct)
    {
        db.Set<Persistence.Models.Emails>().Add(row);
        await db.SaveChangesAsync(ct);
    }
}