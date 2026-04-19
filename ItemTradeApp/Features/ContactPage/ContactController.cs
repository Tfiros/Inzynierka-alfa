using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.ContactPage.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.ContactPage;

[ApiController]
[AllowAnonymous]
[Route("[controller]")]
public sealed class ContactController(IContactPageService contactService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Result<string>>> Send([FromBody] ContactDTO request, CancellationToken ct)
    {
        var result = await contactService.SendAsync(request, ct);
        return result.ToActionResult();
    }
}