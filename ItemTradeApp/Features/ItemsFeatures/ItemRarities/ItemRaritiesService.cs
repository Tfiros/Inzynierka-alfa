using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.ItemsFeatures.ItemRarities;
using ItemTradeApp.Features.ItemsFeatures.ItemRarities.DTOs;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.ItemsFeatures.ItemRarities;

public interface IItemRarityService
{
    Task<Result<ItemRarityListResponse>> GetDropdownAsync(int gameId, string? searchText, CancellationToken ct);

    Task<Result<PagedResponse<ItemRarityDTO>>> GetPagedAsync(
        int gameId,
        int page,
        int pageSize,
        string? searchText,
        CancellationToken ct);

    Task<Result<int>> CreateAsync(CreateItemRarityRequest request, CancellationToken ct);
    Task<Result<object?>> UpdateNameAsync(int id, UpdateItemRarityRequest request, CancellationToken ct);
    Task<Result<object?>> SoftDeleteAsync(int id, CancellationToken ct);
}

public sealed class ItemRarityService(IItemRarityRepository repo) : IItemRarityService
{
    public async Task<Result<ItemRarityListResponse>> GetDropdownAsync(
        int gameId,
        string? searchText,
        CancellationToken ct)
    {
        if (gameId <= 0)
            return new Result<ItemRarityListResponse>(false, ResultStatus.BadRequest, null, "GameId is required.");

        if (!await repo.GameExistsAsync(gameId, ct))
            return new Result<ItemRarityListResponse>(false, ResultStatus.NotFound, null, "Game not found.");

        var items = await repo.SearchForDropdownAsync(gameId, searchText, ct);

        var dto = items
            .Select(x => new ItemRarityDTO(x.ID, x.RarityName))
            .ToList();

        return new Result<ItemRarityListResponse>(
            true, ResultStatus.Success, new ItemRarityListResponse(dto), null);
    }

    public async Task<Result<PagedResponse<ItemRarityDTO>>> GetPagedAsync(
        int gameId,
        int page,
        int pageSize,
        string? searchText,
        CancellationToken ct)
    {
        if (gameId <= 0)
            return new Result<PagedResponse<ItemRarityDTO>>(false, ResultStatus.BadRequest, null, "GameId is required.");

        if (!await repo.GameExistsAsync(gameId, ct))
            return new Result<PagedResponse<ItemRarityDTO>>(false, ResultStatus.NotFound, null, "Game not found.");

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 50) pageSize = 50;

        var (items, totalCount) = await repo.GetPagedAsync(gameId, page, pageSize, searchText, ct);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var response = new PagedResponse<ItemRarityDTO>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Elements = items.Select(x => new ItemRarityDTO(x.ID, x.RarityName)).ToList()
        };

        return new Result<PagedResponse<ItemRarityDTO>>(true, ResultStatus.Success, response, null);
    }

    public async Task<Result<int>> CreateAsync(CreateItemRarityRequest request, CancellationToken ct)
    {
        if (request.GameId <= 0)
            return new Result<int>(false, ResultStatus.BadRequest, 0, "GameId is required.");

        var name = (request.RarityName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return new Result<int>(false, ResultStatus.BadRequest, 0, "RarityName is required.");

        if (name.Length > 20)
            return new Result<int>(false, ResultStatus.BadRequest, 0, "RarityName max 20 chars.");

        if (!await repo.GameExistsAsync(request.GameId, ct))
            return new Result<int>(false, ResultStatus.NotFound, 0, "Game not found.");

        if (await repo.ExistsActiveByNameAsync(request.GameId, name, ct))
            return new Result<int>(false, ResultStatus.Conflict, 0, "Rarity name already exists for this game.");

        var entity = new ItemRarity
        {
            GameId = request.GameId,
            RarityName = name,
            IsDeleted = false
        };

        await repo.AddAsync(entity, ct);
        await repo.SaveChangesAsync(ct);

        return new Result<int>(true, ResultStatus.Created, entity.ID, null);
    }

    public async Task<Result<object?>> UpdateNameAsync(int id, UpdateItemRarityRequest request, CancellationToken ct)
    {
        if (id <= 0)
            return new Result<object?>(false, ResultStatus.BadRequest, null, "Id is required.");

        var name = (request.RarityName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return new Result<object?>(false, ResultStatus.BadRequest, null, "RarityName is required.");

        if (name.Length > 20)
            return new Result<object?>(false, ResultStatus.BadRequest, null, "RarityName max 20 chars.");

        var entity = await repo.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return new Result<object?>(false, ResultStatus.NotFound, null, "Item rarity not found.");

        if (string.Equals(entity.RarityName, name, StringComparison.Ordinal))
            return new Result<object?>(true, ResultStatus.NoContent, null, null);

        if (await repo.ExistsActiveByNameAsync(entity.GameId, name, ct))
            return new Result<object?>(false, ResultStatus.Conflict, null, "Rarity name already exists for this game.");

        entity.RarityName = name;
        await repo.SaveChangesAsync(ct);

        return new Result<object?>(true, ResultStatus.NoContent, null, null);
    }

    public async Task<Result<object?>> SoftDeleteAsync(int id, CancellationToken ct)
    {
        if (id <= 0)
            return new Result<object?>(false, ResultStatus.BadRequest, null, "Id is required.");
        var rarity = await repo.GetByIdWithNoTrackAsync(id, ct);
        if (rarity is null)
            return new Result<object?>(false, ResultStatus.NotFound, null,
                "No ItemRarity for provided id has been found.");
        await repo.SoftDeleteCascadeAsync(id, ct);

        return new Result<object?>(true, ResultStatus.NoContent, null, null);
    }

}
