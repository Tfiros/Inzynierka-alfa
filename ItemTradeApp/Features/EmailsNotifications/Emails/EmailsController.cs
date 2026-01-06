using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.EmaillsNotifications.Emails.Contracts;
using ItemTradeApp.Features.EmaillsNotifications.Emails.Services;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.EmailsNotifications.Emails;

[ApiController]
[Route("[controller]")]
public sealed class EmailsController(IEmailDispatcher dispatcher) : ControllerBase
{
    [HttpPost("enqueue")]
    public async Task<ActionResult<Result<object>>> Enqueue([FromBody] EmailSendRequest req, CancellationToken ct)
    {
        await dispatcher.EnqueueAsync(
            new EmailJob(req.UserId, req.Subject, req.HtmlBody, req.TextBody),
            ct);
        var result = Result<object>.Success(null, "Email enqueued");
        
        return result.ToActionResult();
    }
}