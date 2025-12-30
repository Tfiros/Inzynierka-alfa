using ItemTradeApp.ExceptionsHandling;
using ItemTradeApp.Features.ItemsFeatures.Genres.DTOs;
using ItemTradeApp.Features.ItemsFeatures.Shared;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.ItemsFeatures.Genres;

public interface IGenresService
{
    Task<Result<GenreDTO>> CreateAsync(CreateOrUpdateGenreRequest req, CancellationToken ct);
    Task<Result<GenreDTO>> UpdateAsync(int id, CreateOrUpdateGenreRequest req, CancellationToken ct);
    Task<Result<string>> SoftDeleteAsync(int id, CancellationToken ct);

    Task<Result<PagedResponse<GenreDTO>>> GetPagedAsync(int page, int pageSize, string? searchText, CancellationToken ct);
    Task<Result<DropdownResponse>> GetGenresForDropdownAsync(string? searchText, CancellationToken ct);
}

public sealed class GenresService(IGenresRepository repo) : IGenresService
{
    public async Task<Result<GenreDTO>> CreateAsync(CreateOrUpdateGenreRequest req, CancellationToken ct)
    {
        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result<GenreDTO>.BadRequest("Name is required.");

        if (await repo.ExistsActiveByNameAsync(name, ct))
            return Result<GenreDTO>.Conflict("A genre with the same name already exists.");

        var entity = new Genre
        {
            Name = name,
            IsDeleted = false
        };

        await repo.AddAsync(entity, ct);
        await repo.SaveChangesAsync(ct);

        return Result<GenreDTO>.Created(new GenreDTO(entity.ID, entity.Name));
    }

    public async Task<Result<GenreDTO>> UpdateAsync(int id, CreateOrUpdateGenreRequest req, CancellationToken ct)
    {
        var entity = await repo.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return Result<GenreDTO>.NotFound("Genre doesn't exist.");

        var name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result<GenreDTO>.BadRequest("Genre name is required.");

        if (await repo.ExistsActiveByNameAsync(name, ct))
            return Result<GenreDTO>.Conflict("A genre with the same name already exists.");

        entity.Name = name;
        await repo.SaveChangesAsync(ct);

        return Result<GenreDTO>.Success(new GenreDTO(entity.ID, entity.Name));
    }

    public async Task<Result<string>> SoftDeleteAsync(int id, CancellationToken ct)
    {
        var entity = await repo.GetByIdWithNoTrackAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return Result<string>.NoContent();

        await repo.SoftDeleteCascadeAsync(id, ct);

        return Result<string>.NoContent("Genre deleted.");
    }

    public async Task<Result<DropdownResponse>> GetGenresForDropdownAsync(string? searchText, CancellationToken ct)
    {
        var entities = await repo.GetGenresForDropdownAsync(searchText, ct);
        if (entities.Count == 0)
            return Result<DropdownResponse>.NotFound("No genre found.");

        var genreDtos = entities.Select(x => new DropdownDTO(x.ID, x.Name)).ToList();
        var res = new DropdownResponse(genreDtos);

        return Result<DropdownResponse>.Success(res, "Genres found.");
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

        return Result<PagedResponse<GenreDTO>>.Success(response);
    }
}
