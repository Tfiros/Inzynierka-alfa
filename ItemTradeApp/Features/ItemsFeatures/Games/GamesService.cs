using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.ItemsFeatures.Games.DTOs;
using ItemTradeApp.Features.ItemsFeatures.Genres;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.ItemsFeatures.Games;

public interface IGamesService
{
    Task<Result<GameResponse>> CreateAsync(CreateGameRequest req, CancellationToken ct);
    Task<Result<GameResponse>> UpdateAsync(int id, UpdateGameRequest req, CancellationToken ct);
    Task<Result<object?>> SoftDeleteAsync(int id, CancellationToken ct);
    Task<Result<PagedResponse<GameResponse>>> GetPagedAsync(int page, int pageSize, int genreId, string? searchText, CancellationToken ct);
    Task<Result<List<GameResponse>>> GetGamesForDropdownAsync(string? searchText, CancellationToken ct);
}

public sealed class GamesService(
    IGamesRepository gamesRepo,
    IGenresRepository genresRepo
) : IGamesService
{  
    public async Task<Result<List<GameResponse>>> GetGamesForDropdownAsync(string? searchText , CancellationToken ct)
    {
        var games = await gamesRepo.GetGamesForDropdown(searchText, ct);
        
        var data = games.Select(ToResponse).ToList();
        return new Result<List<GameResponse>>(true, ResultStatus.Success, data, null);
    }
    public async Task<Result<GameResponse>> CreateAsync(CreateGameRequest req, CancellationToken ct)
    {
        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return new Result<GameResponse>(false, ResultStatus.BadRequest, null, "Name is required.");

        if (req.GenreId <= 0)
            return new Result<GameResponse>(false, ResultStatus.BadRequest, null, "GenreId is required.");

        var genre = await genresRepo.GetByIdAsync(req.GenreId, ct);
        if (genre is null || genre.IsDeleted)
            return new Result<GameResponse>(false, ResultStatus.NotFound, null, "Provided genre does not exist.");
        if (await gamesRepo.ExistsByNameAsync(name, ct))
            return new Result<GameResponse>(false, ResultStatus.Conflict, null, "There is already a game with this name.");
        var entity = new Game
        {
            Name = name,
            Genre_ID = genre.ID,
            Photo_URL = "",
            IsDeleted = false
        };

        await gamesRepo.AddAsync(entity, ct);
        await gamesRepo.SaveChangesAsync(ct);

        return new Result<GameResponse>(
            true,
            ResultStatus.Created,
            ToResponse(entity),
            null
        );
    }

    public async Task<Result<GameResponse>> UpdateAsync(int id, UpdateGameRequest req, CancellationToken ct)
    {
        if (id <= 0)
            return new Result<GameResponse>(false, ResultStatus.BadRequest, null, "Id is zero or a negative number.");

        var entity = await gamesRepo.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return new Result<GameResponse>(false, ResultStatus.NotFound, null, "Game doesn't exist.");

        var changed = false;

        var newName = (req.Name ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(newName) &&
            !string.Equals(entity.Name, newName, StringComparison.Ordinal))
        {
            if (await gamesRepo.ExistsByNameAsync(newName, ct))
                return new Result<GameResponse>(false, ResultStatus.Conflict, null, "There is already a game with this name.");

            entity.Name = newName;
            changed = true;
        }

        if (req.GenreId > 0 && entity.Genre_ID != req.GenreId)
        {
            var genre = await genresRepo.GetByIdAsync(req.GenreId, ct);
            if (genre is null || genre.IsDeleted)
                return new Result<GameResponse>(false, ResultStatus.NotFound, null, "Chosen genre doesn't exist.");

            entity.Genre_ID = genre.ID;
            changed = true;
        }

        if (changed)
            await gamesRepo.SaveChangesAsync(ct);

        return new Result<GameResponse>(true, ResultStatus.Success, ToResponse(entity), null);
    }

    public async Task<Result<object?>> SoftDeleteAsync(int id, CancellationToken ct)
    {
        var entity = await gamesRepo.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return new Result<object?>(true, ResultStatus.NoContent, null, null);

        entity.IsDeleted = true;
        await gamesRepo.SaveChangesAsync(ct);

        return new Result<object?>(true, ResultStatus.NoContent, null, null);
    }
    public async Task<Result<PagedResponse<GameResponse>>> GetPagedAsync(int page, int pageSize,int genreId,  string? searchText, CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;
        
        if (genreId <= 0)
            return new Result<PagedResponse<GameResponse>>(false, ResultStatus.BadRequest, null, "GenreId is required.");
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

        return new Result<PagedResponse<GameResponse>>(true, ResultStatus.Success, response, null);
    }
    private static GameResponse ToResponse(Game g)
        => new GameResponse(g.Name, g.Photo_URL,g.Genre.Name);
}

