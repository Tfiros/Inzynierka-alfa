using System.Security.Claims;
using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Shared.Notifications.DTOs;
using ItemTradeApp.Features.Shared.Notifications.Repositories;

namespace ItemTradeApp.Features.Shared.Notifications.Services;

public interface INotificationsService
{
    Task<Result<GetNotificationsResponse>> GetNotificationsAsync(
        ClaimsPrincipal user,
        int take,
        DateTimeOffset? cursorCreatedAt,
        int? cursorId,
        CancellationToken ct);

    Task<Result<object>> MarkReadAsync(
        ClaimsPrincipal user,
        int notificationId,
        CancellationToken ct);

    Task<Result<object>> MarkReadManyAsync(
        ClaimsPrincipal user,
        MarkReadManyRequest req,
        CancellationToken ct);

    Task<Result<object>> MarkReadAllAsync(
        ClaimsPrincipal user,
        CancellationToken ct);

    Task<Result<object>> DeleteAsync(
        ClaimsPrincipal user,
        int notificationId,
        CancellationToken ct);
}

public sealed class NotificationsService(
    INotificationsRepository repo,
    IUserIdentityRepository identityRepo) : INotificationsService
{
    private async Task<int?> ResolveUserIdAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var auth0UserId = Auth0IdHandler.GetUserId(user);

        if (string.IsNullOrWhiteSpace(auth0UserId))
            return null;

        var trimmedAuth0UserId = Auth0IdHandler.Trim(auth0UserId);

        return await identityRepo.GetUserIdByAuth0IdAsync(trimmedAuth0UserId, ct);
    }

    public async Task<Result<GetNotificationsResponse>> GetNotificationsAsync(
        ClaimsPrincipal user,
        int take,
        DateTimeOffset? cursorCreatedAt,
        int? cursorId,
        CancellationToken ct)
    {
        var userId = await ResolveUserIdAsync(user, ct);
        if (userId is null)
            return Result<GetNotificationsResponse>.Unauthorized("No userId mapping.");

        take = take <= 0 ? 20 : take;
        take = Math.Min(take, 50);

        var notifications = await repo.GetForUserCursorAsync(
            userId.Value,
            take + 1,
            cursorCreatedAt,
            cursorId,
            ct);

        var hasMore = notifications.Count > take;

        var pageItems = notifications
            .Take(take)
            .ToList();

        var lastItem = pageItems.LastOrDefault();

        var response = new GetNotificationsResponse(
            Items: pageItems
                .Select(x => new NotificationDto(
                    x.Id,
                    x.Title,
                    x.Message,
                    x.CreatedAt,
                    x.ReadAt,
                    x.ReadAt is not null))
                .ToList(),
            NextCursorCreatedAt: hasMore ? lastItem?.CreatedAt : null,
            NextCursorId: hasMore ? lastItem?.Id : null,
            HasMore: hasMore);

        return Result<GetNotificationsResponse>.Success(response);
    }

    public async Task<Result<object>> MarkReadAsync(
        ClaimsPrincipal user,
        int notificationId,
        CancellationToken ct)
    {
        if (notificationId <= 0)
            return Result<object>.BadRequest("Invalid notification id.");

        var userId = await ResolveUserIdAsync(user, ct);
        if (userId is null)
            return Result<object>.Unauthorized("No userId mapping.");

        var notification = await repo.GetByIdAsync(notificationId, ct);
        if (notification is null)
            return Result<object>.NotFound("Notification not found.");

        if (notification.UserId != userId.Value)
            return Result<object>.Unauthorized("You cannot modify someone else's notification.");

        if (notification.ReadAt is null)
        {
            notification.ReadAt = DateTimeOffset.UtcNow;
            await repo.SaveChangesAsync(ct);
        }

        return Result<object>.Success(null, "Notification marked as read.");
    }

    public async Task<Result<object>> MarkReadManyAsync(
        ClaimsPrincipal user,
        MarkReadManyRequest req,
        CancellationToken ct)
    {
        var userId = await ResolveUserIdAsync(user, ct);
        if (userId is null)
            return Result<object>.Unauthorized("No userId mapping.");

        var ids = (req.Ids ?? [])
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return Result<object>.BadRequest("Ids must not be empty.");

        var updated = await repo.MarkReadManyAsync(
            userId.Value,
            ids,
            DateTimeOffset.UtcNow,
            ct);

        return Result<object>.Success(new { updated }, "Notifications marked as read.");
    }

    public async Task<Result<object>> MarkReadAllAsync(
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = await ResolveUserIdAsync(user, ct);
        if (userId is null)
            return Result<object>.Unauthorized("No userId mapping.");

        var updated = await repo.MarkReadAllAsync(
            userId.Value,
            DateTimeOffset.UtcNow,
            ct);

        return Result<object>.Success(new { updated }, "All notifications marked as read.");
    }

    public async Task<Result<object>> DeleteAsync(
        ClaimsPrincipal user,
        int notificationId,
        CancellationToken ct)
    {
        if (notificationId <= 0)
            return Result<object>.BadRequest("Invalid notification id.");

        var userId = await ResolveUserIdAsync(user, ct);
        if (userId is null)
            return Result<object>.Unauthorized("No userId mapping.");

        var deleted = await repo.SoftDeleteAsync(
            userId.Value,
            notificationId,
            ct);

        if (deleted == 0)
            return Result<object>.NotFound("Notification not found.");

        return Result<object>.Success(null, "Notification deleted.");
    }
}