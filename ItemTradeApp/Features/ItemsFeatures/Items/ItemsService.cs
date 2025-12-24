using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.ItemsFeatures.Games;
using ItemTradeApp.Features.ItemsFeatures.ItemRarities;
using ItemTradeApp.Features.ItemsFeatures.Items.DTOs;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.ItemsFeatures.Items;

public interface IItemsService
{
    Task<Result<ItemResponse>> CreateAsync(CreateItemRequest req, CancellationToken ct);
    Task<Result<ItemResponse>> UpdateAsync(int id, UpdateItemRequest req, CancellationToken ct);
    Task<Result<string>> SoftDeleteAsync(int id, CancellationToken ct);

    Task<Result<PagedResponse<ItemResponse>>> GetPagedAsync(int gameId, int page, int pageSize, string? searchText, CancellationToken ct);
}

public sealed class ItemsService(
    IItemsRepository itemsRepo,
    IGamesRepository gamesRepo,
    IItemRarityRepository itemRarityRepo
) : IItemsService
{
    public async Task<Result<ItemResponse>> CreateAsync(CreateItemRequest req, CancellationToken ct)
    {
        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result<ItemResponse>.BadRequest("Name is required.");

        if (req.GameId <= 0)
            return Result<ItemResponse>.BadRequest("GameId is required.");

        if (req.ItemRarityId <= 0)
            return Result<ItemResponse>.BadRequest("ItemRarityId is required.");

        var game = await gamesRepo.GetByIdAsync(req.GameId, ct);
        if (game is null || game.IsDeleted)
            return Result<ItemResponse>.NotFound("Provided game doesn't exist.");

        var itemRarity = await itemRarityRepo.GetByIdAsync(req.ItemRarityId, ct);
        if (itemRarity is null || itemRarity.IsDeleted || !game.ItemRarities.Contains(itemRarity))
            return Result<ItemResponse>.NotFound("Provided itemRarity doesn't exist.");

        if (await itemsRepo.ExistsByNameAsync(name, ct))
            return Result<ItemResponse>.BadRequest("Item with the same name already exists.");

        var entity = new Item
        {
            Name = name,
            Game_ID = game.ID,
            ItemRarityId = itemRarity.ID,
            EstimatedTokenValue = req.EstimatedTokenValue,
            Photo_URL = "",
            IsDeleted = false
        };

        await itemsRepo.AddAsync(entity, ct);
        await itemsRepo.SaveChangesAsync(ct);

        return Result<ItemResponse>.Created(ToResponse(entity));
    }

    public async Task<Result<ItemResponse>> UpdateAsync(int id, UpdateItemRequest req, CancellationToken ct)
    {
        if (id <= 0)
            return Result<ItemResponse>.BadRequest("Id is equal to zero or is a negative number.");

        var entity = await itemsRepo.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return Result<ItemResponse>.NotFound("Item doesn't exist.");

        var changed = false;

        var newName = (req.Name ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(newName) &&
            !string.Equals(entity.Name, newName, StringComparison.Ordinal))
        {
            if (await itemsRepo.ExistsByNameAsync(newName, ct))
                return Result<ItemResponse>.Conflict("Item with the same name already exists.");

            entity.Name = newName;
            changed = true;
        }

        if (req.EstimatedTokenValue > 0 && entity.EstimatedTokenValue != req.EstimatedTokenValue)
        {
            entity.EstimatedTokenValue = req.EstimatedTokenValue;
            changed = true;
        }

        if (req.RarityItemId > 0 && entity.ItemRarityId != req.RarityItemId)
        {
            var itemRarity = await itemRarityRepo.GetByIdAsync(req.RarityItemId, ct);
            if (itemRarity is null || itemRarity.IsDeleted)
                return Result<ItemResponse>.NotFound("Provided rarity doesn't exist.");

            entity.ItemRarityId = itemRarity.ID;
            changed = true;
        }

        if (changed)
            await itemsRepo.SaveChangesAsync(ct);

        return Result<ItemResponse>.Success(ToResponse(entity));
    }

    public async Task<Result<string>> SoftDeleteAsync(int id, CancellationToken ct)
    {
        var entity = await itemsRepo.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return Result<string>.NoContent();

        entity.IsDeleted = true;
        await itemsRepo.SaveChangesAsync(ct);

        return Result<string>.NoContent("Deleted.");
    }

    public async Task<Result<PagedResponse<ItemResponse>>> GetPagedAsync(int gameId, int page, int pageSize, string? searchText, CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        if (gameId <= 0)
            return Result<PagedResponse<ItemResponse>>.BadRequest("GameId is required.");

        var (entities, totalCount) = await itemsRepo.GetPagedAsync(gameId, page, pageSize, searchText, ct);

        var items = entities.Select(ToResponse).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var response = new PagedResponse<ItemResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Elements = items
        };

        return Result<PagedResponse<ItemResponse>>.Success(response);
    }

    private static ItemResponse ToResponse(Item i)
        => new(i.ID, i.Name, i.Photo_URL, i.EstimatedTokenValue, i.Game.ID, i.Game.Name);
}
