using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.ItemsFeatures.Genres.DTOs;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.ItemsFeatures.Genres;
public interface IGenresService
{
    Task<Result<GenreDTO>> CreateAsync(CreateOrUpdateGenreRequest req, CancellationToken ct);
    Task<Result<GenreDTO>> UpdateAsync(int id, CreateOrUpdateGenreRequest req, CancellationToken ct);
    Task<Result<object?>> SoftDeleteAsync(int id, CancellationToken ct);
    Task<Result<PagedResponse<GenreDTO>>> GetPagedAsync(int page, int pageSize, string? searchText, CancellationToken ct);

    Task<Result<GenresListResponse>> GetGenresForDropdownAsync(string? searchText, CancellationToken ct);
}

public sealed class GenresService(IGenresRepository repo) : IGenresService
{
    public async Task<Result<GenreDTO>> CreateAsync(CreateOrUpdateGenreRequest req, CancellationToken ct)
    {
        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return new Result<GenreDTO>(false, ResultStatus.BadRequest, null, "Name is required.");

        if (await repo.ExistsActiveByNameAsync(name, ct))
            return new Result<GenreDTO>(false, ResultStatus.Conflict, null, "A genre with the same name already exists.");

        var entity = new Genre
        {
            Name = name,
            IsDeleted = false
        };

        await repo.AddAsync(entity, ct);
        await repo.SaveChangesAsync(ct);

        var dto = new GenreDTO(entity.ID,entity.Name);
        return new Result<GenreDTO>(true, ResultStatus.Created, dto, null);
    }

    public async Task<Result<GenreDTO>> UpdateAsync(int id, CreateOrUpdateGenreRequest req, CancellationToken ct)
    {
        var entity = await repo.GetByIdAsync(id, ct);
        if (entity is null)
            return new Result<GenreDTO>(false, ResultStatus.NotFound, null, "Genre doesn't exist.");
        
        if (entity.IsDeleted)
            return new Result<GenreDTO>(false, ResultStatus.NotFound, null, "Genre doesn't exist.");

        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return new Result<GenreDTO>(false, ResultStatus.BadRequest, null, "Genre name is required.");
        if (await repo.ExistsActiveByNameAsync(name, ct))
            return new Result<GenreDTO>(false, ResultStatus.Conflict, null, "A genre with the same name already exists.");
        entity.Name = name;
        await repo.SaveChangesAsync(ct);

        return new Result<GenreDTO>(true, ResultStatus.Success, new GenreDTO(entity.ID, entity.Name), null);
    }

    public async Task<Result<object?>> SoftDeleteAsync(int id, CancellationToken ct)
    {
        var entity = await repo.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return new Result<object?>(true, ResultStatus.NoContent, null, null);

        entity.IsDeleted = true;
        await repo.SaveChangesAsync(ct);

        return new Result<object?>(true, ResultStatus.NoContent, null, null);
    }

    public async Task<Result<GenresListResponse>> GetGenresForDropdownAsync(string? searchText, CancellationToken ct)
    {
        var entities = await repo.GetGenresForDropdownAsync(searchText,ct);
        if (entities.Count == 0) 
            return new Result<GenresListResponse>(false, ResultStatus.NotFound, null, "No genre found.");
        var genreDtos = entities.Select(entity => new GenreDTO(entity.ID, entity.Name)).ToList();
        var res = new GenresListResponse(genreDtos);
        return new Result<GenresListResponse>(true, ResultStatus.Success, res, "Genres found.");
    }
    public async Task<Result<PagedResponse<GenreDTO>>> GetPagedAsync(int page, int pageSize, string? searchText, CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var (entities, totalCount) = await repo.GetPagedAsync(page, pageSize, searchText, ct);

        var items = entities.Select(e => new GenreDTO(e.ID, e.Name)).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var response = new PagedResponse<GenreDTO>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Elements = items
        };

        return new Result<PagedResponse<GenreDTO>>(true, ResultStatus.Success, response, null);
    }
    
}

