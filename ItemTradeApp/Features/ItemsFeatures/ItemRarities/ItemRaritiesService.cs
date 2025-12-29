using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.ItemsFeatures.ItemRarities.DTOs;
using ItemTradeApp.Features.Shared.DTOs;

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
    Task<Result<string>> UpdateNameAsync(int id, UpdateItemRarityRequest request, CancellationToken ct);
    Task<Result<string>> SoftDeleteAsync(int id, CancellationToken ct);
}

public sealed class ItemRarityService(IItemRarityRepository repo) : IItemRarityService
{
    public async Task<Result<ItemRarityListResponse>> GetDropdownAsync(int gameId, string? searchText, CancellationToken ct)
    {
        if (gameId <= 0)
            return Result<ItemRarityListResponse>.BadRequest("GameId is required.");

        if (!await repo.GameExistsAsync(gameId, ct))
            return Result<ItemRarityListResponse>.NotFound("Game not found.");

        var items = await repo.SearchForDropdownAsync(gameId, searchText, ct);

        var dto = items.Select(x => new ItemRarityDTO(x.ID, x.RarityName)).ToList();
        return Result<ItemRarityListResponse>.Success(new ItemRarityListResponse(dto));
    }

    public async Task<Result<PagedResponse<ItemRarityDTO>>> GetPagedAsync(
        int gameId, int page, int pageSize, string? searchText, CancellationToken ct)
    {
        if (gameId <= 0)
            return Result<PagedResponse<ItemRarityDTO>>.BadRequest("GameId is required.");

        if (!await repo.GameExistsAsync(gameId, ct))
            return Result<PagedResponse<ItemRarityDTO>>.NotFound("Game not found.");

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

        return Result<PagedResponse<ItemRarityDTO>>.Success(response);
    }

    public async Task<Result<int>> CreateAsync(CreateItemRarityRequest request, CancellationToken ct)
    {
        if (request.GameId <= 0)
            return Result<int>.BadRequest("GameId is required.");

        var name = (request.RarityName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result<int>.BadRequest("RarityName is required.");

        if (name.Length > 20)
            return Result<int>.BadRequest("RarityName max 20 chars.");

        if (!await repo.GameExistsAsync(request.GameId, ct))
            return Result<int>.NotFound("Game not found.");

        if (await repo.ExistsActiveByNameAsync(request.GameId, name, ct))
            return Result<int>.Conflict("Rarity name already exists for this game.");

        var entity = new Persistence.Models.ItemRarity
        {
            GameId = request.GameId,
            RarityName = name,
            IsDeleted = false
        };

        await repo.AddAsync(entity, ct);
        await repo.SaveChangesAsync(ct);

        return Result<int>.Created(entity.ID);
    }

    public async Task<Result<string>> UpdateNameAsync(int id, UpdateItemRarityRequest request, CancellationToken ct)
    {
        if (id <= 0)
            return Result<string>.BadRequest("Id is required.");

        var name = (request.RarityName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result<string>.BadRequest("RarityName is required.");

        if (name.Length > 20)
            return Result<string>.BadRequest("RarityName max 20 chars.");

        var entity = await repo.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return Result<string>.NotFound("Item rarity not found.");

        if (string.Equals(entity.RarityName, name, StringComparison.Ordinal))
            return Result<string>.NoContent();

        if (await repo.ExistsActiveByNameAsync(entity.GameId, name, ct))
            return Result<string>.Conflict("Rarity name already exists for this game.");

        entity.RarityName = name;
        await repo.SaveChangesAsync(ct);

        return Result<string>.NoContent("Updated.");
    }

    public async Task<Result<string>> SoftDeleteAsync(int id, CancellationToken ct)
    {
        if (id <= 0)
            return Result<string>.BadRequest("Id is required.");

        var rarity = await repo.GetByIdWithNoTrackAsync(id, ct);
        if (rarity is null)
            return Result<string>.NotFound("No ItemRarity for provided id has been found.");

        await repo.SoftDeleteCascadeAsync(id, ct);

        return Result<string>.NoContent("Deleted.");
    }
}
