using ItemTradeApp.Features.ItemsManagement.Genres;
using ItemTradeApp.Features.ItemsManagement.Genres.DTOs;
using ItemTradeApp.Persistence.Models;
using JetBrains.Annotations;
using Moq;

namespace ItemTradeApp.UnitTests.Features.ItemsManagement.Genres;

[TestSubject(typeof(GenresService))]
public class GenresServiceTest
{
    private readonly Mock<IGenresRepository> _repo = new();
    private readonly GenresService _service;

    public GenresServiceTest()
    {
        _service = new GenresService(_repo.Object);
    }

    [Fact]
    public async Task CreateAsync_WhenNameIsEmpty_ReturnsBadRequest()
    {
        var req = new CreateOrUpdateGenreRequest("   ");

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Name is required.", result.Message);

        _repo.Verify(x => x.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(x => x.AddAsync(It.IsAny<Genre>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenGenreWithNameAlreadyExists_ReturnsConflict()
    {
        var req = new CreateOrUpdateGenreRequest("FPS");

        _repo
            .Setup(x => x.GetByNameAsync("FPS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Genre
            {
                ID = 1,
                Name = "FPS",
                IsDeleted = false
            });

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("There is already a game with this name.", result.Message);

        _repo.Verify(x => x.AddAsync(It.IsAny<Genre>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenGenreWithNameExistsButIsDeleted_CreatesNewGenre()
    {
        var req = new CreateOrUpdateGenreRequest("FPS");

        _repo
            .Setup(x => x.GetByNameAsync("FPS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Genre
            {
                ID = 1,
                Name = "FPS",
                IsDeleted = true
            });

        _repo
            .Setup(x => x.AddAsync(It.IsAny<Genre>(), It.IsAny<CancellationToken>()))
            .Callback<Genre, CancellationToken>((genre, _) => genre.ID = 10)
            .Returns(Task.CompletedTask);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(10, result.Data!.id);
        Assert.Equal("FPS", result.Data.Name);

        _repo.Verify(x => x.AddAsync(
            It.Is<Genre>(g =>
                g.Name == "FPS" &&
                !g.IsDeleted),
            It.IsAny<CancellationToken>()), Times.Once);

        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenValidRequest_TrimsNameCreatesGenreAndSaves()
    {
        var req = new CreateOrUpdateGenreRequest("  FPS  ");

        _repo
            .Setup(x => x.GetByNameAsync("FPS", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Genre?)null);

        _repo
            .Setup(x => x.AddAsync(It.IsAny<Genre>(), It.IsAny<CancellationToken>()))
            .Callback<Genre, CancellationToken>((genre, _) => genre.ID = 123)
            .Returns(Task.CompletedTask);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(123, result.Data!.id);
        Assert.Equal("FPS", result.Data.Name);

        _repo.Verify(x => x.AddAsync(
            It.Is<Genre>(g =>
                g.Name == "FPS" &&
                !g.IsDeleted),
            It.IsAny<CancellationToken>()), Times.Once);

        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenGenreDoesNotExist_ReturnsNotFound()
    {
        var req = new CreateOrUpdateGenreRequest("FPS");

        _repo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Genre?)null);

        var result = await _service.UpdateAsync(1, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Genre doesn't exist.", result.Message);

        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenGenreIsDeleted_ReturnsNotFound()
    {
        var req = new CreateOrUpdateGenreRequest("FPS");

        _repo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Genre
            {
                ID = 1,
                Name = "Old",
                IsDeleted = true
            });

        var result = await _service.UpdateAsync(1, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Genre doesn't exist.", result.Message);

        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenNameIsEmpty_ReturnsBadRequest()
    {
        var req = new CreateOrUpdateGenreRequest("   ");

        _repo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Genre
            {
                ID = 1,
                Name = "Old",
                IsDeleted = false
            });

        var result = await _service.UpdateAsync(1, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Genre name is required.", result.Message);

        _repo.Verify(x => x.GetByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenNameAlreadyExists_ReturnsConflict()
    {
        var req = new CreateOrUpdateGenreRequest("RPG");

        _repo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Genre
            {
                ID = 1,
                Name = "FPS",
                IsDeleted = false
            });

        _repo
            .Setup(x => x.GetByNameAsync("RPG", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Genre
            {
                ID = 2,
                Name = "RPG",
                IsDeleted = false
            });

        var result = await _service.UpdateAsync(1, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("There is already a game with this name.", result.Message);

        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenValidRequest_UpdatesGenreAndSaves()
    {
        var req = new CreateOrUpdateGenreRequest("  RPG  ");

        var entity = new Genre
        {
            ID = 1,
            Name = "FPS",
            IsDeleted = false
        };

        _repo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        _repo
            .Setup(x => x.GetByNameAsync("RPG", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Genre?)null);

        var result = await _service.UpdateAsync(1, req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data!.id);
        Assert.Equal("RPG", result.Data.Name);
        Assert.Equal("RPG", entity.Name);

        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SoftDeleteAsync_WhenGenreDoesNotExist_ReturnsNoContentAndDoesNothing()
    {
        _repo
            .Setup(x => x.GetByIdWithNoTrackAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Genre?)null);

        var result = await _service.SoftDeleteAsync(1, CancellationToken.None);

        Assert.True(result.IsSuccess);

        _repo.Verify(x => x.SoftDeleteCascadeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteAsync_WhenGenreIsAlreadyDeleted_ReturnsNoContentAndDoesNothing()
    {
        _repo
            .Setup(x => x.GetByIdWithNoTrackAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Genre
            {
                ID = 1,
                Name = "FPS",
                IsDeleted = true
            });

        var result = await _service.SoftDeleteAsync(1, CancellationToken.None);

        Assert.True(result.IsSuccess);

        _repo.Verify(x => x.SoftDeleteCascadeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteAsync_WhenGenreExists_CallsSoftDeleteCascade()
    {
        _repo
            .Setup(x => x.GetByIdWithNoTrackAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Genre
            {
                ID = 1,
                Name = "FPS",
                IsDeleted = false
            });

        var result = await _service.SoftDeleteAsync(1, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Genre deleted.", result.Message);

        _repo.Verify(x => x.SoftDeleteCascadeAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetGenresForDropdownAsync_WhenNoGenresFound_ReturnsNotFound()
    {
        _repo
            .Setup(x => x.GetGenresForDropdownAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Genre>());

        var result = await _service.GetGenresForDropdownAsync("abc", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("No genre found.", result.Message);
    }

    [Fact]
    public async Task GetGenresForDropdownAsync_WhenGenresExist_ReturnsMappedDropdown()
    {
        _repo
            .Setup(x => x.GetGenresForDropdownAsync("f", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Genre>
            {
                new() { ID = 1, Name = "FPS" },
                new() { ID = 2, Name = "Fighting" }
            });

        var result = await _service.GetGenresForDropdownAsync("f", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("Genres found.", result.Message);
        Assert.Equal(2, result.Data!.Items.Count);
        Assert.Contains(result.Data.Items, x => x.id == 1 && x.Name == "FPS");
        Assert.Contains(result.Data.Items, x => x.id == 2 && x.Name == "Fighting");
    }

    [Fact]
    public async Task GetPagedAsync_WhenPageAndPageSizeAreInvalid_NormalizesValues()
    {
        _repo
            .Setup(x => x.GetPagedAsync(1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new List<Genre>
                {
                    new() { ID = 1, Name = "FPS" }
                },
                1
            ));

        var result = await _service.GetPagedAsync(0, 0, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data!.Page);
        Assert.Equal(10, result.Data.PageSize);
        Assert.Equal(1, result.Data.TotalCount);
        Assert.Equal(1, result.Data.TotalPages);
        Assert.Single(result.Data.Elements);
        Assert.Equal(1, result.Data.Elements[0].id);
        Assert.Equal("FPS", result.Data.Elements[0].Name);
    }

    [Fact]
    public async Task GetPagedAsync_WhenPageSizeIsGreaterThan100_LimitsPageSizeTo100()
    {
        _repo
            .Setup(x => x.GetPagedAsync(2, 100, "rpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Genre>(), 0));

        var result = await _service.GetPagedAsync(2, 999, "rpg", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Page);
        Assert.Equal(100, result.Data.PageSize);
        Assert.Equal(0, result.Data.TotalCount);
        Assert.Equal(0, result.Data.TotalPages);

        _repo.Verify(x => x.GetPagedAsync(
            2,
            100,
            "rpg",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_WhenDataExists_ReturnsMappedPagedResponse()
    {
        _repo
            .Setup(x => x.GetPagedAsync(2, 2, "g", It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new List<Genre>
                {
                    new() { ID = 3, Name = "RPG" },
                    new() { ID = 4, Name = "Strategy" }
                },
                5
            ));

        var result = await _service.GetPagedAsync(2, 2, "g", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Page);
        Assert.Equal(2, result.Data.PageSize);
        Assert.Equal(5, result.Data.TotalCount);
        Assert.Equal(3, result.Data.TotalPages);
        Assert.Equal(2, result.Data.Elements.Count);

        Assert.Contains(result.Data.Elements, x => x.id == 3 && x.Name == "RPG");
        Assert.Contains(result.Data.Elements, x => x.id == 4 && x.Name == "Strategy");
    }
}