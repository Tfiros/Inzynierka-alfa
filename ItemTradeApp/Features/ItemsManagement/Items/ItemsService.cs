using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.ItemsManagement.Games;
using ItemTradeApp.Features.ItemsManagement.ItemRarities;
using ItemTradeApp.Features.ItemsManagement.Items.DTOs;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Shared.Images;
using ItemTradeApp.Persistence.Models;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.Features.ItemsManagement.Items;

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
    IItemRarityRepository itemRarityRepo,
    IImageService imageService,
    IOptions<S3Folders> foldersOptions
) : IItemsService
{
    
    private readonly S3Folders folders = foldersOptions.Value;
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

        if (await itemsRepo.ExistsByNameAsync(name, req.GameId, ct))
            return Result<ItemResponse>.BadRequest(
                "Item with the same name already exists in this game.");

        string? uploadedPhotoUrl = null;

        try
        {
            uploadedPhotoUrl = req.Image is not null
                ? await imageService.UploadAsync(req.Image, folders.Items, ct)
                : "";

            var entity = new Item
            {
                Name = name,
                Game_ID = game.ID,
                ItemRarityId = itemRarity.ID,
                EstimatedTokenValue = req.EstimatedTokenValue,
                Photo_URL = uploadedPhotoUrl,
                IsDeleted = false
            };

            await itemsRepo.AddAsync(entity, ct);
            await itemsRepo.SaveChangesAsync(ct);

            return Result<ItemResponse>.Created(ToResponse(entity));
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(uploadedPhotoUrl))
                await imageService.DeleteAsync(uploadedPhotoUrl, ct);

            return Result<ItemResponse>.InternalServerError("item_create_failed");
        }
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
            if (await itemsRepo.ExistsByNameAsync(
                    newName,
                    entity.Game_ID,
                    entity.ID,
                    ct))
            {
                return Result<ItemResponse>.Conflict(
                    "Item with the same name already exists in this game.");
            }

            entity.Name = newName;
            changed = true;
        }

        if (req.EstimatedTokenValue > 0 && entity.EstimatedTokenValue != req.EstimatedTokenValue)
        {
            entity.EstimatedTokenValue = req.EstimatedTokenValue;
            changed = true;
        }

        if (req.ItemRarityId > 0 && entity.ItemRarityId != req.ItemRarityId)
        {
            var itemRarity = await itemRarityRepo.GetByIdAsync(req.ItemRarityId, ct);
            if (itemRarity is null || itemRarity.IsDeleted)
                return Result<ItemResponse>.NotFound("Provided rarity doesn't exist.");

            entity.ItemRarityId = itemRarity.ID;
            changed = true;
        }
        
        if (req.Image is not null)
        {
            var oldPhotoUrl = entity.Photo_URL;

            var newPhotoUrl = await imageService.UploadAsync(
                req.Image,
                folders.Items,
                ct);

            entity.Photo_URL = newPhotoUrl;

            if (!string.IsNullOrWhiteSpace(oldPhotoUrl))
                await imageService.DeleteAsync(oldPhotoUrl, ct);

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

        if (!string.IsNullOrWhiteSpace(entity.Photo_URL))
            await imageService.DeleteAsync(entity.Photo_URL, ct);

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
        => new(
            i.ID,
            i.Name,
            i.Photo_URL,
            i.EstimatedTokenValue,
            i.Game.ID,
            i.Game.Name,
            i.ItemRarityId
        );
}
