using ItemTradeApp.Features.ItemsManagement.Games;
using ItemTradeApp.Features.ItemsManagement.ItemRarities;
using ItemTradeApp.Features.ItemsManagement.Items;
using ItemTradeApp.Features.ItemsManagement.Items.DTOs;
using ItemTradeApp.Features.Shared.Images;
using ItemTradeApp.Persistence.Models;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;

namespace ItemTradeApp.UnitTests.Features.ItemsManagement.Items;

[TestSubject(typeof(ItemsService))]
public class ItemsServiceTest
{
    private readonly Mock<IItemsRepository> _itemsRepo = new();
    private readonly Mock<IGamesRepository> _gamesRepo = new();
    private readonly Mock<IItemRarityRepository> _itemRarityRepo = new();
    private readonly Mock<IImageService> _imageService = new();

    private readonly ItemsService _service;

    public ItemsServiceTest()
    {
        var folders = Options.Create(new S3Folders
        {
            Items = "items"
        });

        _service = new ItemsService(
            _itemsRepo.Object,
            _gamesRepo.Object,
            _itemRarityRepo.Object,
            _imageService.Object,
            folders);
    }

    [Fact]
    public async Task CreateAsync_WhenNameIsEmpty_ReturnsBadRequest()
    {
        var req = new CreateItemRequest(
            "   ",
            100,
            1,
            1,
            null);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Name is required.", result.Message);

        _gamesRepo.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _itemsRepo.Verify(x => x.AddAsync(It.IsAny<Item>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenGameIdIsInvalid_ReturnsBadRequest()
    {
        var req = new CreateItemRequest(
            "AK-47",
            100,
            0,
            1,
            null);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("GameId is required.", result.Message);

        _gamesRepo.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenItemRarityIdIsInvalid_ReturnsBadRequest()
    {
        var req = new CreateItemRequest(
            "AK-47",
            100,
            1,
            0,
            null);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ItemRarityId is required.", result.Message);

        _gamesRepo.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenGameDoesNotExist_ReturnsNotFound()
    {
        var req = new CreateItemRequest(
            "AK-47",
            100,
            1,
            1,
            null);

        _gamesRepo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Provided game doesn't exist.", result.Message);

        _itemRarityRepo.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _itemsRepo.Verify(x => x.AddAsync(It.IsAny<Item>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenGameIsDeleted_ReturnsNotFound()
    {
        var req = new CreateItemRequest(
            "AK-47",
            100,
            1,
            1,
            null);

        _gamesRepo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Game
            {
                ID = 1,
                Name = "CS2",
                IsDeleted = true
            });

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Provided game doesn't exist.", result.Message);

        _itemRarityRepo.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenItemRarityDoesNotExist_ReturnsNotFound()
    {
        var req = new CreateItemRequest(
            "AK-47",
            100,
            1,
            10,
            null);

        var game = ExistingGame();

        _gamesRepo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _itemRarityRepo
            .Setup(x => x.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ItemRarity?)null);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Provided itemRarity doesn't exist.", result.Message);

        _itemsRepo.Verify(x => x.AddAsync(It.IsAny<Item>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenItemRarityIsDeleted_ReturnsNotFound()
    {
        var req = new CreateItemRequest(
            "AK-47",
            100,
            1,
            10,
            null);

        var game = ExistingGame();

        _gamesRepo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _itemRarityRepo
            .Setup(x => x.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ItemRarity
            {
                ID = 10,
                GameId = 1,
                RarityName = "Rare",
                IsDeleted = true
            });

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Provided itemRarity doesn't exist.", result.Message);

        _itemsRepo.Verify(x => x.AddAsync(It.IsAny<Item>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenItemRarityDoesNotBelongToGame_ReturnsNotFound()
    {
        var req = new CreateItemRequest(
            "AK-47",
            100,
            1,
            99,
            null);

        var game = ExistingGame();

        var rarityFromAnotherGame = new ItemRarity
        {
            ID = 99,
            GameId = 2,
            RarityName = "Legendary",
            IsDeleted = false
        };

        _gamesRepo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _itemRarityRepo
            .Setup(x => x.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rarityFromAnotherGame);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Provided itemRarity doesn't exist.", result.Message);

        _itemsRepo.Verify(x => x.AddAsync(It.IsAny<Item>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenItemWithSameNameExists_ReturnsBadRequest()
    {
        var game = ExistingGame();
        var rarity = game.ItemRarities.First();

        var req = new CreateItemRequest(
            "AK-47",
            100,
            game.ID,
            rarity.ID,
            null);

        _gamesRepo
            .Setup(x => x.GetByIdAsync(game.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _itemRarityRepo
            .Setup(x => x.GetByIdAsync(rarity.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rarity);

        _itemsRepo
            .Setup(x => x.ExistsByNameAsync("AK-47", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Item with the same name already exists.", result.Message);

        _itemsRepo.Verify(x => x.AddAsync(It.IsAny<Item>(), It.IsAny<CancellationToken>()), Times.Never);
        _itemsRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenValidRequestWithoutImage_CreatesItemAndSaves()
    {
        var game = ExistingGame();
        var rarity = game.ItemRarities.First();

        var req = new CreateItemRequest(
            "  AK-47  ",
            150,
            game.ID,
            rarity.ID,
            null);

        _gamesRepo
            .Setup(x => x.GetByIdAsync(game.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _itemRarityRepo
            .Setup(x => x.GetByIdAsync(rarity.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rarity);

        _itemsRepo
            .Setup(x => x.ExistsByNameAsync("AK-47", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _itemsRepo
            .Setup(x => x.AddAsync(It.IsAny<Item>(), It.IsAny<CancellationToken>()))
            .Callback<Item, CancellationToken>((item, _) =>
            {
                item.ID = 123;
                item.Game = game;
            })
            .Returns(Task.CompletedTask);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(123, result.Data!.Id);
        Assert.Equal("AK-47", result.Data.Name);
        Assert.Equal("", result.Data.Photo_URL);
        Assert.Equal(150, result.Data.EstimatedTokenValue);
        Assert.Equal(game.ID, result.Data.GameId);
        Assert.Equal(game.Name, result.Data.GameName);
        Assert.Equal(rarity.ID, result.Data.ItemRarityId);

        _itemsRepo.Verify(x => x.AddAsync(
            It.Is<Item>(i =>
                i.Name == "AK-47" &&
                i.Game_ID == game.ID &&
                i.ItemRarityId == rarity.ID &&
                i.EstimatedTokenValue == 150 &&
                i.Photo_URL == "" &&
                !i.IsDeleted),
            It.IsAny<CancellationToken>()), Times.Once);

        _itemsRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _imageService.Verify(x => x.UploadAsync(
            It.IsAny<IFormFile>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenValidRequestWithImage_UploadsImageCreatesItemAndSaves()
    {
        var image = Mock.Of<IFormFile>();
        var game = ExistingGame();
        var rarity = game.ItemRarities.First();

        var req = new CreateItemRequest(
            "AK-47",
            150,
            game.ID,
            rarity.ID,
            image);

        _gamesRepo
            .Setup(x => x.GetByIdAsync(game.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _itemRarityRepo
            .Setup(x => x.GetByIdAsync(rarity.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rarity);

        _itemsRepo
            .Setup(x => x.ExistsByNameAsync("AK-47", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _imageService
            .Setup(x => x.UploadAsync(image, "items", It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-photo-url");

        _itemsRepo
            .Setup(x => x.AddAsync(It.IsAny<Item>(), It.IsAny<CancellationToken>()))
            .Callback<Item, CancellationToken>((item, _) =>
            {
                item.ID = 123;
                item.Game = game;
            })
            .Returns(Task.CompletedTask);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-photo-url", result.Data!.Photo_URL);

        _imageService.Verify(x => x.UploadAsync(image, "items", It.IsAny<CancellationToken>()), Times.Once);
        _itemsRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenImageUploadedButRepositoryThrows_DeletesUploadedImageAndReturnsInternalServerError()
    {
        var image = Mock.Of<IFormFile>();
        var game = ExistingGame();
        var rarity = game.ItemRarities.First();

        var req = new CreateItemRequest(
            "AK-47",
            150,
            game.ID,
            rarity.ID,
            image);

        _gamesRepo
            .Setup(x => x.GetByIdAsync(game.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _itemRarityRepo
            .Setup(x => x.GetByIdAsync(rarity.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rarity);

        _itemsRepo
            .Setup(x => x.ExistsByNameAsync("AK-47", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _imageService
            .Setup(x => x.UploadAsync(image, "items", It.IsAny<CancellationToken>()))
            .ReturnsAsync("uploaded-photo-url");

        _itemsRepo
            .Setup(x => x.AddAsync(It.IsAny<Item>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("db failed"));

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("item_create_failed", result.Message);

        _imageService.Verify(x => x.DeleteAsync("uploaded-photo-url", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenIdIsInvalid_ReturnsBadRequest()
    {
        var req = new UpdateItemRequest(
            "",
            0,
            0,
            null);

        var result = await _service.UpdateAsync(0, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Id is equal to zero or is a negative number.", result.Message);

        _itemsRepo.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenItemDoesNotExist_ReturnsNotFound()
    {
        var req = new UpdateItemRequest(
            "AK-47",
            150,
            1,
            null);

        _itemsRepo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Item?)null);

        var result = await _service.UpdateAsync(1, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Item doesn't exist.", result.Message);

        _itemsRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenItemIsDeleted_ReturnsNotFound()
    {
        var entity = ExistingItem();
        entity.IsDeleted = true;

        var req = new UpdateItemRequest(
            "AK-47",
            150,
            1,
            null);

        _itemsRepo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _service.UpdateAsync(1, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Item doesn't exist.", result.Message);

        _itemsRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenNewNameAlreadyExists_ReturnsConflict()
    {
        var entity = ExistingItem();

        var req = new UpdateItemRequest(
            "AWP",
            0,
            0,
            null);

        _itemsRepo
            .Setup(x => x.GetByIdAsync(entity.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        _itemsRepo
            .Setup(x => x.ExistsByNameAsync("AWP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.UpdateAsync(entity.ID, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Item with the same name already exists.", result.Message);

        _itemsRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenNewRarityDoesNotExist_ReturnsNotFound()
    {
        var entity = ExistingItem();

        var req = new UpdateItemRequest(
            "",
            0,
            99,
            null);

        _itemsRepo
            .Setup(x => x.GetByIdAsync(entity.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        _itemRarityRepo
            .Setup(x => x.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ItemRarity?)null);

        var result = await _service.UpdateAsync(entity.ID, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Provided rarity doesn't exist.", result.Message);

        _itemsRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenNewRarityIsDeleted_ReturnsNotFound()
    {
        var entity = ExistingItem();

        var req = new UpdateItemRequest(
            "",
            0,
            99,
            null);

        _itemsRepo
            .Setup(x => x.GetByIdAsync(entity.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        _itemRarityRepo
            .Setup(x => x.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ItemRarity
            {
                ID = 99,
                GameId = entity.Game_ID,
                RarityName = "Epic",
                IsDeleted = true
            });

        var result = await _service.UpdateAsync(entity.ID, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Provided rarity doesn't exist.", result.Message);

        _itemsRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenNameEstimatedValueAndRarityChanged_UpdatesEntityAndSaves()
    {
        var entity = ExistingItem();

        var newRarity = new ItemRarity
        {
            ID = 2,
            GameId = entity.Game_ID,
            RarityName = "Epic",
            IsDeleted = false
        };

        var req = new UpdateItemRequest(
            "  AWP  ",
            300,
            newRarity.ID,
            null);

        _itemsRepo
            .Setup(x => x.GetByIdAsync(entity.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        _itemsRepo
            .Setup(x => x.ExistsByNameAsync("AWP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _itemRarityRepo
            .Setup(x => x.GetByIdAsync(newRarity.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newRarity);

        var result = await _service.UpdateAsync(entity.ID, req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("AWP", entity.Name);
        Assert.Equal(300, entity.EstimatedTokenValue);
        Assert.Equal(newRarity.ID, entity.ItemRarityId);

        _itemsRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenImageChanged_UploadsNewImageDeletesOldImageAndSaves()
    {
        var image = Mock.Of<IFormFile>();
        var entity = ExistingItem();
        entity.Photo_URL = "old-photo-url";

        var req = new UpdateItemRequest(
            "",
            0,
            0,
            image);

        _itemsRepo
            .Setup(x => x.GetByIdAsync(entity.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        _imageService
            .Setup(x => x.UploadAsync(image, "items", It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-photo-url");

        var result = await _service.UpdateAsync(entity.ID, req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-photo-url", entity.Photo_URL);
        Assert.Equal("new-photo-url", result.Data!.Photo_URL);

        _imageService.Verify(x => x.UploadAsync(image, "items", It.IsAny<CancellationToken>()), Times.Once);
        _imageService.Verify(x => x.DeleteAsync("old-photo-url", It.IsAny<CancellationToken>()), Times.Once);
        _itemsRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenNothingChanged_DoesNotSave()
    {
        var entity = ExistingItem();

        var req = new UpdateItemRequest(
            entity.Name,
            entity.EstimatedTokenValue,
            entity.ItemRarityId,
            null);

        _itemsRepo
            .Setup(x => x.GetByIdAsync(entity.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _service.UpdateAsync(entity.ID, req, CancellationToken.None);

        Assert.True(result.IsSuccess);

        _itemsRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteAsync_WhenItemDoesNotExist_ReturnsNoContentAndDoesNothing()
    {
        _itemsRepo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Item?)null);

        var result = await _service.SoftDeleteAsync(1, CancellationToken.None);

        Assert.True(result.IsSuccess);

        _imageService.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _itemsRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteAsync_WhenItemIsAlreadyDeleted_ReturnsNoContentAndDoesNothing()
    {
        var entity = ExistingItem();
        entity.IsDeleted = true;

        _itemsRepo
            .Setup(x => x.GetByIdAsync(entity.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _service.SoftDeleteAsync(entity.ID, CancellationToken.None);

        Assert.True(result.IsSuccess);

        _imageService.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _itemsRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteAsync_WhenItemExists_MarksDeletedDeletesImageAndSaves()
    {
        var entity = ExistingItem();
        entity.Photo_URL = "photo-url";

        _itemsRepo
            .Setup(x => x.GetByIdAsync(entity.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _service.SoftDeleteAsync(entity.ID, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Deleted.", result.Message);
        Assert.True(entity.IsDeleted);

        _imageService.Verify(x => x.DeleteAsync("photo-url", It.IsAny<CancellationToken>()), Times.Once);
        _itemsRepo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_WhenGameIdIsInvalid_ReturnsBadRequest()
    {
        var result = await _service.GetPagedAsync(0, 1, 10, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("GameId is required.", result.Message);

        _itemsRepo.Verify(x => x.GetPagedAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPagedAsync_WhenPageAndPageSizeAreInvalid_NormalizesValues()
    {
        var item = ExistingItem();

        _itemsRepo
            .Setup(x => x.GetPagedAsync(item.Game_ID, 1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new List<Item> { item },
                1
            ));

        var result = await _service.GetPagedAsync(item.Game_ID, 0, 0, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data!.Page);
        Assert.Equal(10, result.Data.PageSize);
        Assert.Equal(1, result.Data.TotalCount);
        Assert.Equal(1, result.Data.TotalPages);
        Assert.Single(result.Data.Elements);
        Assert.Equal(item.ID, result.Data.Elements[0].Id);
        Assert.Equal(item.Name, result.Data.Elements[0].Name);
        Assert.Equal(item.Game.Name, result.Data.Elements[0].GameName);
    }

    [Fact]
    public async Task GetPagedAsync_WhenPageSizeIsGreaterThan100_LimitsPageSizeTo100()
    {
        _itemsRepo
            .Setup(x => x.GetPagedAsync(1, 2, 100, "ak", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Item>(), 0));

        var result = await _service.GetPagedAsync(1, 2, 999, "ak", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Page);
        Assert.Equal(100, result.Data.PageSize);

        _itemsRepo.Verify(x => x.GetPagedAsync(
            1,
            2,
            100,
            "ak",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_WhenDataExists_ReturnsMappedPagedResponse()
    {
        var first = ExistingItem();
        var second = ExistingItem();
        second.ID = 2;
        second.Name = "AWP";
        second.EstimatedTokenValue = 300;

        _itemsRepo
            .Setup(x => x.GetPagedAsync(1, 2, 2, "a", It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new List<Item> { first, second },
                5
            ));

        var result = await _service.GetPagedAsync(1, 2, 2, "a", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Page);
        Assert.Equal(2, result.Data.PageSize);
        Assert.Equal(5, result.Data.TotalCount);
        Assert.Equal(3, result.Data.TotalPages);
        Assert.Equal(2, result.Data.Elements.Count);

        Assert.Contains(result.Data.Elements, x => x.Id == 1 && x.Name == "AK-47");
        Assert.Contains(result.Data.Elements, x => x.Id == 2 && x.Name == "AWP");
    }

    private static Game ExistingGame()
    {
        var rarity = new ItemRarity
        {
            ID = 1,
            GameId = 1,
            RarityName = "Rare",
            IsDeleted = false
        };

        var game = new Game
        {
            ID = 1,
            Name = "CS2",
            IsDeleted = false,
            ItemRarities = new List<ItemRarity>()
        };

        game.ItemRarities.Add(rarity);
        rarity.Game = game;

        return game;
    }

    private static Item ExistingItem()
    {
        var game = ExistingGame();
        var rarity = game.ItemRarities.First();

        return new Item
        {
            ID = 1,
            Name = "AK-47",
            Photo_URL = "",
            EstimatedTokenValue = 150,
            Game_ID = game.ID,
            Game = game,
            ItemRarityId = rarity.ID,
            ItemRarity = rarity,
            IsDeleted = false
        };
    }
}