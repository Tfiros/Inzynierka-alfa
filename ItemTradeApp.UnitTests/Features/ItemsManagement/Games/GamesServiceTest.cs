using ItemTradeApp.Features.ItemsManagement.Games;
using ItemTradeApp.Features.ItemsManagement.Games.DTOs;
using ItemTradeApp.Features.ItemsManagement.Genres;
using ItemTradeApp.Features.ItemsManagement.ItemRarities;
using ItemTradeApp.Features.ItemsManagement.Shared;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Shared.Images;
using ItemTradeApp.Persistence.Models;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;

namespace ItemTradeApp.UnitTests.Features.ItemsManagement.Games;

[TestSubject(typeof(GamesService))]
public class GamesServiceTest
{
    private readonly Mock<IGamesRepository> _gamesRepo = new();
    private readonly Mock<IGenresRepository> _genresRepo = new();
    private readonly Mock<IItemRarityRepository> _rarityRepo = new();
    private readonly Mock<IImageService> _imageService = new();

    private readonly GamesService _service;

    public GamesServiceTest()
    {
        var folders = Options.Create(new S3Folders
        {
            Games = "games"
        });

        _service = new GamesService(
            _gamesRepo.Object,
            _genresRepo.Object,
            _rarityRepo.Object,
            _imageService.Object,
            folders);
    }

    [Fact]
    public async Task CreateAsync_WhenNameIsEmpty_ReturnsBadRequest()
    {
        var req = new CreateGameRequest(
            "   ",
            1,
            ["Common"],
            null);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Name is required.", result.Message);

        _genresRepo.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _gamesRepo.Verify(x => x.CreateWithRaritiesAsync(
            It.IsAny<Game>(),
            It.IsAny<List<ItemRarity>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenGenreIdIsInvalid_ReturnsBadRequest()
    {
        var req = new CreateGameRequest(
            "Counter Strike",
            0,
            ["Common"],
            null);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("GenreId is required.", result.Message);
    }

    [Fact]
    public async Task CreateAsync_WhenRaritiesAreEmpty_ReturnsBadRequest()
    {
        var req = new CreateGameRequest(
            "Counter Strike",
            1,
            [" ", "", null!],
            null);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("At least one item rarity is required.", result.Message);
    }

    [Fact]
    public async Task CreateAsync_WhenGenreDoesNotExist_ReturnsNotFound()
    {
        var req = new CreateGameRequest(
            "Counter Strike",
            99,
            ["Common"],
            null);

        _genresRepo
            .Setup(x => x.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Genre?)null);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Provided genre does not exist.", result.Message);
    }

    [Fact]
    public async Task CreateAsync_WhenGameWithNameAlreadyExists_ReturnsConflict()
    {
        var req = new CreateGameRequest(
            "Counter Strike",
            1,
            ["Common"],
            null);

        _genresRepo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Genre { ID = 1, Name = "FPS", IsDeleted = false });

        _gamesRepo
            .Setup(x => x.GetByNameAsync("Counter Strike", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Game { ID = 10, Name = "Counter Strike", IsDeleted = false });

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("There is already a game with this name.", result.Message);

        _gamesRepo.Verify(x => x.CreateWithRaritiesAsync(
            It.IsAny<Game>(),
            It.IsAny<List<ItemRarity>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenValidRequest_CreatesGameWithDistinctTrimmedRarities()
    {
        var req = new CreateGameRequest(
            "  Counter Strike  ",
            1,
            [" Common ", "common", "Rare"],
            null);

        var genre = new Genre
        {
            ID = 1,
            Name = "FPS",
            IsDeleted = false
        };

        _genresRepo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(genre);

        _gamesRepo
            .Setup(x => x.GetByNameAsync("Counter Strike", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        _gamesRepo
            .Setup(x => x.CreateWithRaritiesAsync(
                It.IsAny<Game>(),
                It.IsAny<List<ItemRarity>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game game, List<ItemRarity> _, CancellationToken _) =>
            {
                game.ID = 123;
                game.Genre = genre;
                return game;
            });

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(123, result.Data!.Id);
        Assert.Equal("Counter Strike", result.Data.Name);
        Assert.Equal("FPS", result.Data.GamesGenre);

        _gamesRepo.Verify(x => x.CreateWithRaritiesAsync(
            It.Is<Game>(g =>
                g.Name == "Counter Strike" &&
                g.Genre_ID == 1 &&
                g.Photo_URL == "" &&
                !g.IsDeleted),
            It.Is<List<ItemRarity>>(rarities =>
                rarities.Count == 2 &&
                rarities.Any(r => r.RarityName == "Common") &&
                rarities.Any(r => r.RarityName == "Rare")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenImageUploadWasDoneButRepositoryThrows_DeletesUploadedImageAndReturnsInternalServerError()
    {
        var image = Mock.Of<IFormFile>();

        var req = new CreateGameRequest(
            "Counter Strike",
            1,
            ["Common"],
            image);

        _genresRepo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Genre { ID = 1, Name = "FPS", IsDeleted = false });

        _gamesRepo
            .Setup(x => x.GetByNameAsync("Counter Strike", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        _imageService
            .Setup(x => x.UploadAsync(image, "games", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uploaded-url");

        _gamesRepo
            .Setup(x => x.CreateWithRaritiesAsync(
                It.IsAny<Game>(),
                It.IsAny<List<ItemRarity>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("db failed"));

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("game_create_failed", result.Message);

        _imageService.Verify(x => x.DeleteAsync("uploaded-url", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenIdIsInvalid_ReturnsBadRequest()
    {
        var req = new UpdateGameRequest(
            "",
            0,
            null);

        var result = await _service.UpdateAsync(0, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Id is zero or a negative number.", result.Message);
    }

    [Fact]
    public async Task UpdateAsync_WhenGameDoesNotExist_ReturnsNotFound()
    {
        var req = new UpdateGameRequest(
            "Counter Strike",
            1,
            null);

        _gamesRepo
            .Setup(x => x.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var result = await _service.UpdateAsync(10, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Game doesn't exist.", result.Message);
    }

    [Fact]
    public async Task UpdateAsync_WhenNameAlreadyExists_ReturnsConflict()
    {
        var entity = ExistingGame();

        _gamesRepo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        _gamesRepo
            .Setup(x => x.GetByNameAsync("Valorant", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Game { ID = 2, Name = "Valorant", IsDeleted = false });

        var req = new UpdateGameRequest(
            "Valorant",
            0,
            null);

        var result = await _service.UpdateAsync(1, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("There is already a game with this name.", result.Message);

        _gamesRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenGenreDoesNotExist_ReturnsNotFound()
    {
        var entity = ExistingGame();

        _gamesRepo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        _genresRepo
            .Setup(x => x.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Genre?)null);

        var req = new UpdateGameRequest(
            "",
            2,
            null);

        var result = await _service.UpdateAsync(1, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Chosen genre doesn't exist.", result.Message);

        _gamesRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenNameAndGenreChanged_UpdatesEntityAndSaves()
    {
        var entity = ExistingGame();

        var newGenre = new Genre
        {
            ID = 2,
            Name = "Shooter",
            IsDeleted = false
        };

        _gamesRepo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        _gamesRepo
            .Setup(x => x.GetByNameAsync("CS2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        _genresRepo
            .Setup(x => x.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newGenre);

        var req = new UpdateGameRequest(
            "CS2",
            2,
            null);

        var result = await _service.UpdateAsync(1, req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("CS2", entity.Name);
        Assert.Equal(2, entity.Genre_ID);

        _gamesRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenImageChanged_UploadsNewImageDeletesOldOneAndSaves()
    {
        var image = Mock.Of<IFormFile>();
        var entity = ExistingGame();
        entity.Photo_URL = "old-url";

        _gamesRepo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        _imageService
            .Setup(x => x.UploadAsync(image, "games", It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-url");

        var req = new UpdateGameRequest(
            "",
            0,
            image);

        var result = await _service.UpdateAsync(1, req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-url", entity.Photo_URL);

        _imageService.Verify(x => x.UploadAsync(image, "games", It.IsAny<CancellationToken>()), Times.Once);
        _imageService.Verify(x => x.DeleteAsync("old-url", It.IsAny<CancellationToken>()), Times.Once);
        _gamesRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenNothingChanged_DoesNotSave()
    {
        var entity = ExistingGame();

        _gamesRepo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var req = new UpdateGameRequest(
            "Counter Strike",
            entity.Genre_ID,
            null);

        var result = await _service.UpdateAsync(1, req, CancellationToken.None);

        Assert.True(result.IsSuccess);

        _gamesRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteAsync_WhenGameDoesNotExist_ReturnsNoContentAndDoesNothing()
    {
        _gamesRepo
            .Setup(x => x.GetByIdWithNoTrackAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var result = await _service.SoftDeleteAsync(1, CancellationToken.None);

        Assert.True(result.IsSuccess);

        _imageService.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _gamesRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteAsync_WhenGameAlreadyDeleted_ReturnsNoContentAndDoesNothing()
    {
        var entity = ExistingGame();
        entity.IsDeleted = true;

        _gamesRepo
            .Setup(x => x.GetByIdWithNoTrackAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _service.SoftDeleteAsync(1, CancellationToken.None);

        Assert.True(result.IsSuccess);

        _imageService.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _gamesRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteAsync_WhenGameExists_MarksDeletedDeletesImageAndSaves()
    {
        var entity = ExistingGame();
        entity.Photo_URL = "photo-url";

        _gamesRepo
            .Setup(x => x.GetByIdWithNoTrackAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _service.SoftDeleteAsync(1, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(entity.IsDeleted);

        _imageService.Verify(x => x.DeleteAsync("photo-url", It.IsAny<CancellationToken>()), Times.Once);
        _gamesRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_WhenGenreIdIsInvalid_ReturnsBadRequest()
    {
        var result = await _service.GetPagedAsync(1, 10, 0, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("GenreId is required.", result.Message);
    }

    [Fact]
    public async Task GetPagedAsync_WhenPageAndPageSizeAreInvalid_NormalizesValues()
    {
        var genre = new Genre
        {
            ID = 1,
            Name = "FPS",
            IsDeleted = false
        };

        _gamesRepo
            .Setup(x => x.GetPagedAsync(1, 1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new List<Game>
                {
                    new()
                    {
                        ID = 1,
                        Name = "Counter Strike",
                        Photo_URL = "",
                        Genre_ID = 1,
                        Genre = genre,
                        IsDeleted = false
                    }
                },
                1
            ));

        var result = await _service.GetPagedAsync(0, 0, 1, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data!.Page);
        Assert.Equal(10, result.Data.PageSize);
        Assert.Equal(1, result.Data.TotalCount);
        Assert.Equal(1, result.Data.TotalPages);
        Assert.Single(result.Data.Elements);
        Assert.Equal("FPS", result.Data.Elements[0].GamesGenre);
    }

    [Fact]
    public async Task GetPagedAsync_WhenPageSizeIsGreaterThan100_LimitsPageSizeTo100()
    {
        _gamesRepo
            .Setup(x => x.GetPagedAsync(1, 2, 100, "cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Game>(), 0));

        var result = await _service.GetPagedAsync(2, 999, 1, "cs", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Page);
        Assert.Equal(100, result.Data.PageSize);

        _gamesRepo.Verify(x => x.GetPagedAsync(
            1,
            2,
            100,
            "cs",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetGamesForDropdownAsync_ReturnsMappedDropdown()
    {
        _gamesRepo
            .Setup(x => x.GetGamesForDropdown("cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Game>
            {
                new() { ID = 1, Name = "Counter Strike" },
                new() { ID = 2, Name = "CS2" }
            });

        var result = await _service.GetGamesForDropdownAsync("cs", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Items.Count);
        Assert.Contains(result.Data.Items, x => x.id == 1 && x.Name == "Counter Strike");
        Assert.Contains(result.Data.Items, x => x.id == 2 && x.Name == "CS2");
    }

    private static Game ExistingGame()
    {
        var genre = new Genre
        {
            ID = 1,
            Name = "FPS",
            IsDeleted = false
        };

        return new Game
        {
            ID = 1,
            Name = "Counter Strike",
            Genre_ID = 1,
            Genre = genre,
            Photo_URL = "",
            IsDeleted = false
        };
    }
}