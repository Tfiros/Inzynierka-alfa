using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Shared.Notifications.Repositories;

public interface INotificationsRepository
{
    Task AddAsync(Notification entity, CancellationToken ct);

    Task<Notification?> GetByIdAsync(int id, CancellationToken ct);

    Task<List<Notification>> GetForUserCursorAsync(
        int userId,
        int take,
        DateTimeOffset? cursorCreatedAt,
        int? cursorId,
        CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);

    Task<int> MarkReadManyAsync(
        int userId,
        IReadOnlyCollection<int> ids,
        DateTimeOffset readAt,
        CancellationToken ct);

    Task<int> MarkReadAllAsync(
        int userId,
        DateTimeOffset readAt,
        CancellationToken ct);

    Task<int> SoftDeleteAsync(
        int userId,
        int notificationId,
        CancellationToken ct);
    Task AddManyAsync(List<Notification> entities, CancellationToken ct);
}

public sealed class NotificationsRepository(AppDbContext db) : INotificationsRepository
{
    public async Task AddAsync(Notification entity, CancellationToken ct)
        => await db.Notifications.AddAsync(entity, ct).AsTask();

    public async Task<Notification?> GetByIdAsync(int id, CancellationToken ct)
        => await db.Notifications
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
    public async Task AddManyAsync(List<Notification> entities, CancellationToken ct)
        => await db.Notifications.AddRangeAsync(entities, ct);
    public async Task<List<Notification>> GetForUserCursorAsync(
        int userId,
        int take,
        DateTimeOffset? cursorCreatedAt,
        int? cursorId,
        CancellationToken ct)
    {
        var query = db.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted);

        if (cursorCreatedAt is not null && cursorId is not null)
        {
            query = query.Where(x =>
                x.CreatedAt < cursorCreatedAt.Value ||
                x.CreatedAt == cursorCreatedAt.Value && x.Id < cursorId.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .ToListAsync(ct);
    }

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
            .Where(n =>
                n.UserId == userId &&
                ids.Contains(n.Id) &&
                n.ReadAt == null &&
                !n.IsDeleted)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAt, readAt), ct);
    }

    public async Task<int> MarkReadAllAsync(
        int userId,
        DateTimeOffset readAt,
        CancellationToken ct)
        => await db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null && !n.IsDeleted)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAt, readAt), ct);

    public async Task<int> SoftDeleteAsync(
        int userId,
        int notificationId,
        CancellationToken ct)
        => await db.Notifications
            .Where(n => n.UserId == userId && n.Id == notificationId && !n.IsDeleted)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true), ct);
}