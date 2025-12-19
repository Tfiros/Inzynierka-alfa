using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.ItemsFeatures.Items.DTOs;
using ItemTradeApp.Features.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Features.ItemsFeatures.Items;

[ApiController]
[Route("[controller]")]
[Authorize(Roles = "Admin")]
public sealed class ItemsController(IItemsService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Result<ItemResponse>>> Create([FromBody] CreateItemRequest req, CancellationToken ct = default)
    {
        var res = await service.CreateAsync(req, ct);
        return res.ToActionResult();
    }
    [HttpGet]
    public async Task<ActionResult<Result<PagedResponse<ItemResponse>>>> GetItems(
        [FromQuery] int gameId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchText = null,
        CancellationToken ct = default)
    {
        var res = await service.GetPagedAsync(gameId,page, pageSize, searchText, ct);
        return res.ToActionResult();
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Result<ItemResponse>>> Update([FromRoute] int id, [FromBody] UpdateItemRequest req, CancellationToken ct= default)
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
    
}