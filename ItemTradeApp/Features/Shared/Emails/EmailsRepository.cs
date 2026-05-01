using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Shared.Emails;

public interface IEmailsRepository
{
    Task SaveSentAsync(Persistence.Models.Email row, CancellationToken ct);
}

public sealed class EmailsRepository(AppDbContext db) : IEmailsRepository
{
    public async Task SaveSentAsync(Email row, CancellationToken ct)
    {
        db.Emails.Add(row);
        await db.SaveChangesAsync(ct);
    }
}