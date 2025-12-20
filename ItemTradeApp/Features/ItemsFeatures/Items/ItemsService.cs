using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.ItemsFeatures.Games;
using ItemTradeApp.Features.ItemsFeatures.Items.DTOs;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.ItemsFeatures.Items;

public interface IItemsService
{
    Task<Result<ItemResponse>> CreateAsync(CreateItemRequest req, CancellationToken ct);
    Task<Result<ItemResponse>> UpdateAsync(int id, UpdateItemRequest req, CancellationToken ct);
    Task<Result<object?>> SoftDeleteAsync(int id, CancellationToken ct);
    Task<Result<PagedResponse<ItemResponse>>> GetPagedAsync(int gameId,int page, int pageSize, string? searchText, CancellationToken ct);
}

public sealed class ItemsService(
    IItemsRepository itemsRepo,
    IGamesRepository gamesRepo
) : IItemsService
{
    public async Task<Result<ItemResponse>> CreateAsync(CreateItemRequest req, CancellationToken ct)
    {
        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return new Result<ItemResponse>(false, ResultStatus.BadRequest, null, "Name is required.");

        if (req.GameId <= 0)
            return new Result<ItemResponse>(false, ResultStatus.BadRequest, null, "GameId is required.");

        var game = await gamesRepo.GetByIdAsync(req.GameId, ct);
        if (game is null || game.IsDeleted)
            return new Result<ItemResponse>(false, ResultStatus.NotFound, null, "Provided game doesn't exist.");
        if (await itemsRepo.ExistsByNameAsync(name, ct))
            return new Result<ItemResponse>(false, ResultStatus.BadRequest, null, "Item with the same name already exists.");
        var entity = new Item
        {
            Name = name,
            Game_ID = game.ID,
            Photo_URL = "",
            IsDeleted = false
        };

        await itemsRepo.AddAsync(entity, ct);
        await itemsRepo.SaveChangesAsync(ct);

        return new Result<ItemResponse>(true, ResultStatus.Created, ToResponse(entity), null);
    }

    public async Task<Result<ItemResponse>> UpdateAsync(int id, UpdateItemRequest req, CancellationToken ct)
    {
        if (id <= 0)
            return new Result<ItemResponse>(false, ResultStatus.BadRequest, null, "Id is equal to zero or is a negative number.");

        var entity = await itemsRepo.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return new Result<ItemResponse>(false, ResultStatus.NotFound, null, "Item doesn't exist.");

        var changed = false;

        var newName = (req.Name ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(newName) &&
            !string.Equals(entity.Name, newName, StringComparison.Ordinal))
        {
            if (await itemsRepo.ExistsByNameAsync(newName, ct))
                return new Result<ItemResponse>(false, ResultStatus.Conflict, null, "Item with the same name already exists.");

            entity.Name = newName;
            changed = true;
        }

        if (req.GameId > 0 && entity.Game_ID != req.GameId)
        {
            var game = await gamesRepo.GetByIdAsync(req.GameId, ct);
            if (game is null || game.IsDeleted)
                return new Result<ItemResponse>(false, ResultStatus.NotFound, null, "Provided game doesn't exist.");

            entity.Game_ID = game.ID;
            changed = true;
        }

        if (changed)
            await itemsRepo.SaveChangesAsync(ct);

        return new Result<ItemResponse>(true, ResultStatus.Success, ToResponse(entity), null);
    }


    public async Task<Result<object?>> SoftDeleteAsync(int id, CancellationToken ct)
    {
        var entity = await itemsRepo.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return new Result<object?>(true, ResultStatus.NoContent, null, null);

        entity.IsDeleted = true;
        await itemsRepo.SaveChangesAsync(ct);

        return new Result<object?>(true, ResultStatus.NoContent, null, null);
    }

    public async Task<Result<PagedResponse<ItemResponse>>> GetPagedAsync( int gameId, int page, int pageSize, string? searchText, CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;
        
        if (gameId <= 0)
            return new Result<PagedResponse<ItemResponse>>(false, ResultStatus.BadRequest, null, "GameId is required.");

        var (entities, totalCount) = await itemsRepo.GetPagedAsync( gameId,page, pageSize, searchText , ct);

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

        return new Result<PagedResponse<ItemResponse>>(true, ResultStatus.Success, response, null);
    }
    private static ItemResponse ToResponse(Item i)
        => new ItemResponse(i.ID, i.Name, i.Photo_URL,i.Game.ID, i.Game.Name);
}