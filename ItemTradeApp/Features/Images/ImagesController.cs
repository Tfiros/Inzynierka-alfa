using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.Images;

[ApiController]
[Route("Images")]
public sealed class ImagesController(
    IImageService imageService) : ControllerBase
{
    [HttpPost("item-photo")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadItemPhoto(
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null)
            return BadRequest("Brak pliku.");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            return BadRequest("Nieobsługiwany format pliku.");
        
        //Sprawdzanie sygnatury pliku, czy zawartość odpowiada rozszerzeniu
        if (!ImageExtensionValidator.IsValidImage(file))
            return BadRequest("Nieprawidłowy plik obrazu.");

        var key = await imageService.UploadAsync(file, "items", ct);
        var url = imageService.GetImageUrl(key);

        return Ok(new
        {
            key,
            url
        });
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(
        [FromQuery] string key,
        CancellationToken ct)
    {
        await imageService.DeleteAsync(key, ct);
        return NoContent();
    }

    [HttpGet]
    public IActionResult GetImageUrl([FromQuery] string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest("Brak klucza pliku.");

        var url = imageService.GetImageUrl(key);

        return Ok(new
        {
            key,
            url
        });
    }
}