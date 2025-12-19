using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.ItemsFeatures.Genres.DTOs;
using ItemTradeApp.Features.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.ItemsFeatures.Genres;

[ApiController]
[Route("[controller]")]
[Authorize(Roles = "Admin")]
public sealed class GenresController(IGenresService service) : ControllerBase
{
    [HttpGet("dropdown")]
    public async Task<ActionResult<Result<GenresListResponse>>> GetGenresForDropdown(string? searchText, CancellationToken ct = default)
    {
        var res = await service.GetGenresForDropdownAsync(searchText, ct);
        return res.ToActionResult();
    }
    [HttpPost]
    public async Task<ActionResult<Result<GenreDTO>>> Create([FromBody] CreateOrUpdateGenreRequest req, CancellationToken ct = default)
    {
        var res = await service.CreateAsync(req, ct);
        return res.ToActionResult();
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Result<GenreDTO>>> Update([FromRoute] int id, [FromBody] CreateOrUpdateGenreRequest req, CancellationToken ct = default)
    {
        var res = await service.UpdateAsync(id, req, ct);
        return res.ToActionResult();
    }

    [HttpDelete("{id:int}/delete")]
    public async Task<ActionResult<Result<object?>>> SoftDelete([FromRoute] int id, CancellationToken ct = default)
    {
        var res = await service.SoftDeleteAsync(id, ct);
        return res.ToActionResult();
    }
    [HttpGet]
    public async Task<ActionResult<Result<PagedResponse<GenreDTO>>>> GetGenres(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchText = null,
        CancellationToken ct = default)
    {
        var res = await service.GetPagedAsync(page, pageSize, searchText, ct);
        return res.ToActionResult();
    }
}