using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.ItemsFeatures.ItemRarities.DTOs;
using ItemTradeApp.Features.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.ItemsFeatures.ItemRarities;

[ApiController]
[Route("[controller]")]
[Authorize(Roles = "Admin")]
public sealed class ItemRarityController(IItemRarityService service) : ControllerBase
{
    [HttpGet("dropdown/{gameId:int}")]
    public async Task<ActionResult<Result<ItemRarityListResponse>>> GetDropdown(
        [FromRoute] int gameId,
        [FromQuery] string? searchText,
        CancellationToken ct = default)
    {
        var res = await service.GetDropdownAsync(gameId, searchText, ct);
        return res.ToActionResult();
    }

    [HttpGet]
    public async Task<ActionResult<Result<PagedResponse<ItemRarityDTO>>>> GetPaged(
        [FromQuery] int gameId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchText = null,
        CancellationToken ct = default)
    {
        var res = await service.GetPagedAsync(gameId, page, pageSize, searchText, ct);
        return res.ToActionResult();
    }

    [HttpPost]
    public async Task<ActionResult<Result<int>>> Create(
        [FromBody] CreateItemRarityRequest request,
        CancellationToken ct = default)
    {
        var res = await service.CreateAsync(request, ct);
        return res.ToActionResult();
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Result<string>>> UpdateName(
        [FromRoute] int id,
        [FromBody] UpdateItemRarityRequest request,
        CancellationToken ct = default)
    {
        var res = await service.UpdateNameAsync(id, request, ct);
        return res.ToActionResult();
    }

    [HttpDelete("{id:int}/delete")]
    public async Task<ActionResult<Result<string>>> SoftDelete(
        [FromRoute] int id,
        CancellationToken ct = default)
    {
        var res = await service.SoftDeleteAsync(id, ct);
        return res.ToActionResult();
    }
}
