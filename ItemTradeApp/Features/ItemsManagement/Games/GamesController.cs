using ItemTradeApp.Features.ItemsManagement.Games.DTOs;
using ItemTradeApp.Features.ItemsManagement.Shared;
using ItemTradeApp.Features.Shared.DTOs;

namespace ItemTradeApp.Features.ItemsManagement.Games;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiResultHandling;

[ApiController]
[Route("[controller]")]
[Authorize(Roles = "Admin")]
public sealed class GamesController(IGamesService service) : ControllerBase
{
    [HttpGet("dropdown")]
    public async Task<ActionResult<Result<DropdownResponse>>> GetGamesForDropdown([FromQuery] string? searchText, CancellationToken ct = default)
    {
        var res = await service.GetGamesForDropdownAsync(searchText, ct);
        return res.ToActionResult();
    }
    [HttpPost]
    public async Task<ActionResult<Result<GameResponse>>> Create(
        [FromForm] CreateGameRequest req,
        CancellationToken ct = default)
    {
        var res = await service.CreateAsync(req, ct);
        return res.ToActionResult();
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Result<GameResponse>>> Update(
        [FromRoute] int id,
        [FromForm] UpdateGameRequest req,
        CancellationToken ct = default)
    {
        var res = await service.UpdateAsync(id, req, ct);
        return res.ToActionResult();
    }

    [HttpDelete("{id:int}/delete")]
    public async Task<ActionResult<Result<string>>> SoftDelete([FromRoute] int id, CancellationToken ct = default)
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
