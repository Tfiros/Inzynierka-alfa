using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.EmailsNotifications.Notifications.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.EmailsNotifications.Notifications;

[ApiController]
[Route("[controller]")]
[Authorize]
public sealed class NotificationsController(INotificationsService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Result<object>>> Create([FromBody] CreateNotificationRequest req, CancellationToken ct)
        => (await service.CreateAsync(req, ct)).ToActionResult();

    [HttpPost("{id:int}/read")]
    public async Task<ActionResult<Result<object>>> MarkRead([FromRoute] int id, CancellationToken ct)
        => (await service.MarkReadAsync(User, id, ct)).ToActionResult();

    [HttpPost("read")]
    public async Task<ActionResult<Result<object>>> MarkReadMany([FromBody] MarkReadManyRequest req, CancellationToken ct)
        => (await service.MarkReadManyAsync(User, req, ct)).ToActionResult();

    [HttpPost("read-all")]
    public async Task<ActionResult<Result<object>>> MarkReadAll(CancellationToken ct)
        => (await service.MarkReadAllAsync(User, ct)).ToActionResult();
}