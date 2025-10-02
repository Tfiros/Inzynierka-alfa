using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class OffersController : ControllerBase
{
    [HttpGet("public")]
    public IActionResult Public() => Ok(new { Message = "Public OK" });
    
    [Authorize]
    [HttpGet("private")]
    public IActionResult Private() => Ok(new { Message = "Private OK (token valid)" });

    [Authorize(Policy = "Admin")]
    [HttpGet("private-scoped")]
    public IActionResult PrivateScoped() => Ok(new { Message = "Private OK (read:OwnOffers)" });
}