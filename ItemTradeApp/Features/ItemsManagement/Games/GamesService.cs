using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.ItemsManagement.Games.DTOs;
using ItemTradeApp.Features.ItemsManagement.Genres;
using ItemTradeApp.Features.ItemsManagement.ItemRarities;
using ItemTradeApp.Features.ItemsManagement.Shared;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.ItemsManagement.Games;

public interface IGamesService
{
    Task<Result<GameResponse>> CreateAsync(CreateGameRequest req, CancellationToken ct);
    Task<Result<GameResponse>> UpdateAsync(int id, UpdateGameRequest req, CancellationToken ct);

    Task<Result<string>> SoftDeleteAsync(int id, CancellationToken ct);

    Task<Result<PagedResponse<GameResponse>>> GetPagedAsync(
        int page, int pageSize, int genreId, string? searchText, CancellationToken ct);

    Task<Result<DropdownResponse>> GetGamesForDropdownAsync(string? searchText, CancellationToken ct);
}

public sealed class GamesService(
    IGamesRepository gamesRepo,
    IGenresRepository genresRepo,
    IItemRarityRepository itemRarityRepo
) : IGamesService
{
    public async Task<Result<DropdownResponse>> GetGamesForDropdownAsync(string? searchText, CancellationToken ct)
    {
        var games = await gamesRepo.GetGamesForDropdown(searchText, ct);

        var data = games.Select(g => new DropdownDTO(g.ID, g.Name)).ToList();
        var res = new DropdownResponse(data);

        return Result<DropdownResponse>.Success(res);
    }

    public async Task<Result<GameResponse>> CreateAsync(CreateGameRequest req, CancellationToken ct)
    {
        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result<GameResponse>.BadRequest("Name is required.");

        if (req.GenreId <= 0)
            return Result<GameResponse>.BadRequest("GenreId is required.");

        var raritiesNames = (req.ItemRaritiesNames ?? new List<string>())
            .Select(r => (r ?? string.Empty).Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (raritiesNames.Count == 0)
            return Result<GameResponse>.BadRequest("At least one item rarity is required.");

        var genre = await genresRepo.GetByIdAsync(req.GenreId, ct);
        if (genre is null || genre.IsDeleted)
            return Result<GameResponse>.NotFound("Provided genre does not exist.");

        var gameOfName = await gamesRepo.GetByNameAsync(name, ct);
        if (gameOfName is not null && !gameOfName.IsDeleted)
        {
            return Result<GameResponse>.Conflict("There is already a game with this name.");
        }
        
        var game = new Game
        {
            Name = name,
            Genre_ID = genre.ID,
            Photo_URL = "",
            IsDeleted = false
        };

        var rarities = raritiesNames.Select(r => new ItemRarity
        {
            RarityName = r,
            IsDeleted = false
        }).ToList();

        var createdGame = await gamesRepo.CreateWithRaritiesAsync(game, rarities, ct);

        return Result<GameResponse>.Created(ToResponse(createdGame), "Game created successfully");
    }

    public async Task<Result<GameResponse>> UpdateAsync(int id, UpdateGameRequest req, CancellationToken ct)
    {
        if (id <= 0)
            return Result<GameResponse>.BadRequest("Id is zero or a negative number.");

        var entity = await gamesRepo.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return Result<GameResponse>.NotFound("Game doesn't exist.");

        var changed = false;

        var newName = (req.Name ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(newName) &&
            !string.Equals(entity.Name, newName, StringComparison.Ordinal))
        {
            var gameOfName = await gamesRepo.GetByNameAsync(newName, ct);
            if (gameOfName is not null && !gameOfName.IsDeleted)
            {
                return Result<GameResponse>.Conflict("There is already a game with this name.");
            }

            entity.Name = newName;
            changed = true;
        }

        if (req.GenreId > 0 && entity.Genre_ID != req.GenreId)
        {
            var genre = await genresRepo.GetByIdAsync(req.GenreId, ct);
            if (genre is null || genre.IsDeleted)
                return Result<GameResponse>.NotFound("Chosen genre doesn't exist.");

            entity.Genre_ID = genre.ID;
            changed = true;
        }

        if (changed)
            await gamesRepo.SaveChangesAsync(ct);

        return Result<GameResponse>.Success(ToResponse(entity));
    }

    public async Task<Result<string>> SoftDeleteAsync(int id, CancellationToken ct)
    {
        var entity = await gamesRepo.GetByIdWithNoTrackAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return Result<string>.NoContent();

        entity.IsDeleted = true;
        await gamesRepo.SaveChangesAsync(ct);

        return Result<string>.NoContent("Game deleted.");
    }

    public async Task<Result<PagedResponse<GameResponse>>> GetPagedAsync(
        int page, int pageSize, int genreId, string? searchText, CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        if (genreId <= 0)
            return Result<PagedResponse<GameResponse>>.BadRequest("GenreId is required.");

        var (entities, totalCount) = await gamesRepo.GetPagedAsync(genreId, page, pageSize, searchText, ct);

        var items = entities.Select(ToResponse).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var response = new PagedResponse<GameResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Elements = items
        };

        return Result<PagedResponse<GameResponse>>.Success(response);
    }

    private static GameResponse ToResponse(Game g)
        => new(g.ID, g.Name, g.Photo_URL, g.Genre.Name);
}
