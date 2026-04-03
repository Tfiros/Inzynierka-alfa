using ItemTradeApp.Features.ContactPage.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.ContactPage;



[ApiController]
[Route("[controller]")]
public sealed class ContactController(IContactPageService contactService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] ContactDTO request, CancellationToken ct)
    {
        try
        {
            await contactService.SendAsync(request, ct);
            return Ok(new { message = "Wiadomość została wysłana." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch
        {
            return StatusCode(500, new { message = "Nie udało się wysłać wiadomości." });
        }
    }
}