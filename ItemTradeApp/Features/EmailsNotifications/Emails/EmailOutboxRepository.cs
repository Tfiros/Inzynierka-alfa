using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.EmaillsNotifications.Emails;

public interface IEmailOutboxRepository
{
    Task SaveSentAsync(EmailOutbox row, CancellationToken ct);
}

public sealed class EmailOutboxRepository(AppDbContext db) : IEmailOutboxRepository
{
    public async Task SaveSentAsync(EmailOutbox row, CancellationToken ct)
    {
        db.Set<EmailOutbox>().Add(row);
        await db.SaveChangesAsync(ct);
    }
}