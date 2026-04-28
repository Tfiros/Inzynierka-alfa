using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Shared.Notifications;
public interface INotificationsRepository
{
    Task AddAsync(Notification entity, CancellationToken ct);
    Task<Notification?> GetByIdAsync(int id, CancellationToken ct);
    Task<List<Notification>> GetForUserAsync(int userId, int take, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task<int> MarkReadManyAsync(int userId, IReadOnlyCollection<int> ids, DateTimeOffset readAt, CancellationToken ct);
    Task<int> MarkReadAllAsync(int userId, DateTimeOffset readAt, CancellationToken ct);
}

public sealed class NotificationsRepository(AppDbContext db) : INotificationsRepository
{
    public async Task AddAsync(Notification entity, CancellationToken ct)
        => await db.Notifications.AddAsync(entity, ct).AsTask();

    public async Task<Notification?> GetByIdAsync(int id, CancellationToken ct)
        => await db.Notifications.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<List<Notification>> GetForUserAsync(int userId, int take, CancellationToken ct)
        => await db.Notifications
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct)
        => await db.SaveChangesAsync(ct);
    public async Task<int> MarkReadManyAsync(
        int userId,
        IReadOnlyCollection<int> ids,
        DateTimeOffset readAt,
        CancellationToken ct)
    {
        if (ids.Count == 0) return 0;
        return await db.Notifications
            .Where(n => n.UserId == userId && ids.Contains(n.Id) && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAt, readAt), ct);
    }

    public async Task<int> MarkReadAllAsync(int userId, DateTimeOffset readAt, CancellationToken ct)
        => await db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAt, readAt), ct);
}