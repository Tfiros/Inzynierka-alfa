using System.Security.Claims;
using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.EmailsNotifications.Notifications.Contracts;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.EmailsNotifications.Notifications;
public interface INotificationsService
{
    Task<Result<object>> CreateAsync(CreateNotificationRequest req, CancellationToken ct);
    Task<Result<object>> MarkReadAsync(ClaimsPrincipal user, int notificationId, CancellationToken ct);
    Task<Result<object>> MarkReadManyAsync(ClaimsPrincipal user, MarkReadManyRequest req, CancellationToken ct);
    Task<Result<object>> MarkReadAllAsync(ClaimsPrincipal user, CancellationToken ct);
}

public sealed class NotificationsService(
    INotificationsRepository repo,
    INotificationsPublisher realtime,
    IUserIdentityRepository identityRepo) : INotificationsService
{
    private async Task<int?> ResolveUserIdAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var auth0UserId = user.FindFirst("sub")?.Value ?? 
                          user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        string trimmedAuth0UserId = auth0UserId.StartsWith("auth0|")
            ? auth0UserId.Substring("auth0|".Length)
            : auth0UserId;
        if (string.IsNullOrWhiteSpace(auth0UserId)) return null;

        return await identityRepo.GetUserIdByAuth0IdAsync(trimmedAuth0UserId, ct);
    }

    public async Task<Result<object>> CreateAsync(CreateNotificationRequest req, CancellationToken ct)
    {
        if (req.UserId <= 0) return Result<object>.BadRequest("Invalid userId.");
        if (string.IsNullOrWhiteSpace(req.Title)) return Result<object>.BadRequest("Title is required.");
        if (string.IsNullOrWhiteSpace(req.Message)) return Result<object>.BadRequest("Message is required.");

        var n = new Notification
        {
            UserId = req.UserId,
            Title = req.Title.Trim(),
            Message = req.Message.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            ReadAt = null
        };

        await repo.AddAsync(n, ct);
        await repo.SaveChangesAsync(ct);

        await realtime.PublishCreatedAsync(
            n.UserId,
            new NotificationPushedDTO(n.Id, n.Title, n.Message, n.CreatedAt),
            ct);
        return Result<object>.Success(null, "Notification created successfully");
    }

    public async Task<Result<object>> MarkReadAsync(ClaimsPrincipal user, int notificationId, CancellationToken ct)
    {
        if (notificationId <= 0) return Result<object>.BadRequest("Invalid notification id.");

        var userId = await ResolveUserIdAsync(user, ct);
        if (userId is null) return Result<object>.Unauthorized("No userId mapping.");

        var n = await repo.GetByIdAsync(notificationId, ct);
        if (n is null) return Result<object>.NotFound("Notification not found.");

        if (n.UserId != userId.Value)
            return Result<object>.Unauthorized("You cannot modify someone else's notification.");

        if (n.ReadAt is null)
        {
            n.ReadAt = DateTimeOffset.UtcNow;
            await repo.SaveChangesAsync(ct);
        }

        return Result<object>.Success(null, "Notification marked as read.");
    }

    public async Task<Result<object>> MarkReadManyAsync(ClaimsPrincipal user, MarkReadManyRequest req, CancellationToken ct)
    {
        var userId = await ResolveUserIdAsync(user, ct);
        if (userId is null) return Result<object>.Unauthorized("No userId mapping.");

        var ids = req.Ids
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return Result<object>.BadRequest("Ids must not be empty.");

        var updated = await repo.MarkReadManyAsync(userId.Value, ids, DateTimeOffset.UtcNow, ct);
        return Result<object>.Success(new { updated }, "Notifications marked as read.");
    }

    public async Task<Result<object>> MarkReadAllAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var userId = await ResolveUserIdAsync(user, ct);
        if (userId is null) return Result<object>.Unauthorized("No userId mapping.");

        var updated = await repo.MarkReadAllAsync(userId.Value, DateTimeOffset.UtcNow, ct);
        return Result<object>.Success(new { updated }, "All notifications marked as read.");
    }
}
