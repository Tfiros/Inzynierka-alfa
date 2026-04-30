using ItemTradeApp.Features.Shared.Notifications.DTOs;
using ItemTradeApp.Features.Shared.Notifications.Repositories;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Shared.Notifications;

public interface INotificationSender
{
    Task SendAsync(
        int userId,
        string title,
        string message,
        CancellationToken ct = default);
    Task SendManyAsync(
        IReadOnlyCollection<int> userIds,
        string title,
        string message,
        CancellationToken ct = default);
}

public sealed class NotificationSender(
    INotificationsRepository repo,
    INotificationsPublisher realtime) : INotificationSender
{
    public async Task SendAsync(
        int userId,
        string title,
        string message,
        CancellationToken ct = default)
    {
        if (userId <= 0)
            throw new ArgumentException("Invalid userId.", nameof(userId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required.", nameof(message));

        var notification = new Notification
        {
            UserId = userId,
            Title = title.Trim(),
            Message = message.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            ReadAt = null,
            IsDeleted = false
        };

        await repo.AddAsync(notification, ct);
        await repo.SaveChangesAsync(ct);

        await realtime.PublishCreatedAsync(
            userId,
            new NotificationPushedDTO(
                notification.Id,
                notification.Title,
                notification.Message,
                notification.CreatedAt),
            ct);
    }
    
    public async Task SendManyAsync(
        IReadOnlyCollection<int> userIds,
        string title,
        string message,
        CancellationToken ct = default)
    {
        var distinctUserIds = userIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        if (distinctUserIds.Count == 0)
            throw new ArgumentException("At least one valid userId is required.", nameof(userIds));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required.", nameof(message));

        var now = DateTimeOffset.UtcNow;

        var notifications = distinctUserIds
            .Select(userId => new Notification
            {
                UserId = userId,
                Title = title.Trim(),
                Message = message.Trim(),
                CreatedAt = now,
                ReadAt = null,
                IsDeleted = false
            })
            .ToList();

        await repo.AddManyAsync(notifications, ct);
        await repo.SaveChangesAsync(ct);

        foreach (var notification in notifications)
        {
            await realtime.PublishCreatedAsync(
                notification.UserId,
                new NotificationPushedDTO(
                    notification.Id,
                    notification.Title,
                    notification.Message,
                    notification.CreatedAt),
                ct);
        }
    }
}