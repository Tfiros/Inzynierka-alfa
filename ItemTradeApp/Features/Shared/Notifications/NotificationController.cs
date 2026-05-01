using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Shared.Notifications.DTOs;
using ItemTradeApp.Features.Shared.Notifications.Services;
using ItemTradeApp.Persistence.Models;
using ItemTradeApp.Resources.NotificationsTemplates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.Shared.Notifications;

[ApiController]
[Route("[controller]")]
[Authorize]
public sealed class NotificationsController(INotificationsService service, INotificationSender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<string>> PerformNotify(CancellationToken ct = default)
    {
        await sender.SendManyAsync( userIds: [2,3],
            NotificationsMessages.CounterOfferAcceptedWithTradeCreation("Sam offer"), ct);
        return Ok("Alles klar");
    }
    [HttpGet]
    public async Task<ActionResult<Result<GetNotificationsResponse>>> GetNotifications(
        [FromQuery] int take = 20,
        [FromQuery] DateTimeOffset? cursorCreatedAt = null,
        [FromQuery] int? cursorId = null,
        CancellationToken ct = default)
        => (await service.GetNotificationsAsync(User, take, cursorCreatedAt, cursorId, ct)).ToActionResult();

    [HttpPost("{id:int}/read")]
    public async Task<ActionResult<Result<object>>> MarkRead(
        [FromRoute] int id,
        CancellationToken ct)
        => (await service.MarkReadAsync(User, id, ct)).ToActionResult();

    [HttpPost("read")]
    public async Task<ActionResult<Result<object>>> MarkReadMany(
        [FromBody] MarkReadManyRequest req,
        CancellationToken ct)
        => (await service.MarkReadManyAsync(User, req, ct)).ToActionResult();

    [HttpPost("read-all")]
    public async Task<ActionResult<Result<object>>> MarkReadAll(CancellationToken ct)
        => (await service.MarkReadAllAsync(User, ct)).ToActionResult();

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<Result<object>>> Delete(
        [FromRoute] int id,
        CancellationToken ct)
        => (await service.DeleteAsync(User, id, ct)).ToActionResult();
}