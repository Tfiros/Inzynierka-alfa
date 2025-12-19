using ItemTradeApp.Features.ItemsFeatures.Games.DTOs;
using ItemTradeApp.Features.Shared.DTOs;

namespace ItemTradeApp.Features.ItemsFeatures.Games;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExceptionsHandling;

[ApiController]
[Route("[controller]")]
[Authorize(Roles = "Admin")]
public sealed class GamesController(IGamesService service) : ControllerBase
{
    [HttpGet("dropdown")]
    public async Task<ActionResult<Result<List<GameResponse>>>> GetGamesForDropdown([FromQuery] string? searchText, CancellationToken ct)
    {
        var res = await service.GetGamesForDropdownAsync(searchText, ct);
        return res.ToActionResult();
    }
    [HttpPost]
    public async Task<ActionResult<Result<GameResponse>>> Create([FromBody] CreateGameRequest req, CancellationToken ct)
    {
        var res = await service.CreateAsync(req, ct);
        return res.ToActionResult();
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Result<GameResponse>>> Update([FromRoute] int id, [FromBody] UpdateGameRequest req, CancellationToken ct)
    {
        var res = await service.UpdateAsync(id, req, ct);
        return res.ToActionResult();
    }

    [HttpDelete("{id:int}/delete")]
    public async Task<ActionResult<Result<object?>>> SoftDelete([FromRoute] int id, CancellationToken ct)
    {
        var res = await service.SoftDeleteAsync(id, ct);
        return res.ToActionResult();
    }
    [HttpGet]
    public async Task<ActionResult<Result<PagedResponse<GameResponse>>>> GetGames(
        [FromQuery] int genreId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchText = null,
        CancellationToken ct = default)
    {
        var res = await service.GetPagedAsync(page, pageSize,genreId, searchText, ct);
        return res.ToActionResult();
    }
}
