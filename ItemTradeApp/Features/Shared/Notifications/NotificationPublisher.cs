using ItemTradeApp.Features.Shared.Notifications.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace ItemTradeApp.Features.Shared.Notifications;

public interface INotificationsPublisher
{
    Task PublishCreatedAsync(int userId, NotificationPushedDTO dto, CancellationToken ct);
}

public sealed class NotificationsPublisher(IHubContext<NotificationsHub> hub)
    : INotificationsPublisher
{
    public Task PublishCreatedAsync(int userId, NotificationPushedDTO dto, CancellationToken ct)
        => hub.Clients.Group($"user:{userId}")
            .SendAsync("notificationCreated", dto, ct);
    
}