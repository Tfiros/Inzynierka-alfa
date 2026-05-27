using ItemTradeApp.Features.ItemsManagement.ItemRarities;
using ItemTradeApp.Features.ItemsManagement.ItemRarities.DTOs;
using ItemTradeApp.Persistence.Models;
using JetBrains.Annotations;
using Moq;

namespace ItemTradeApp.UnitTests.Features.ItemsManagement.ItemRarities;

[TestSubject(typeof(ItemRarityService))]
public class ItemRarityServiceTest
{
    private readonly Mock<IItemRarityRepository> _repo = new();
    private readonly ItemRarityService _service;

    public ItemRarityServiceTest()
    {
        _service = new ItemRarityService(_repo.Object);
    }

    [Fact]
    public async Task GetDropdownAsync_WhenGameIdIsInvalid_ReturnsBadRequest()
    {
        var result = await _service.GetDropdownAsync(0, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("GameId is required.", result.Message);

        _repo.Verify(x => x.GameExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(x => x.SearchForDropdownAsync(
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetDropdownAsync_WhenGameDoesNotExist_ReturnsNotFound()
    {
        _repo
            .Setup(x => x.GameExistsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.GetDropdownAsync(1, "com", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Game not found.", result.Message);

        _repo.Verify(x => x.SearchForDropdownAsync(
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetDropdownAsync_WhenGameExists_ReturnsMappedItems()
    {
        _repo
            .Setup(x => x.GameExistsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repo
            .Setup(x => x.SearchForDropdownAsync(1, "r", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ItemRarity>
            {
                new() { ID = 1, GameId = 1, RarityName = "Rare" },
                new() { ID = 2, GameId = 1, RarityName = "Ultra Rare" }
            });

        var result = await _service.GetDropdownAsync(1, "r", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Items.Count);
        Assert.Contains(result.Data.Items, x => x.Id == 1 && x.Name == "Rare");
        Assert.Contains(result.Data.Items, x => x.Id == 2 && x.Name == "Ultra Rare");
    }

    [Fact]
    public async Task GetPagedAsync_WhenGameIdIsInvalid_ReturnsBadRequest()
    {
        var result = await _service.GetPagedAsync(0, 1, 10, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("GameId is required.", result.Message);

        _repo.Verify(x => x.GameExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(x => x.GetPagedAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPagedAsync_WhenGameDoesNotExist_ReturnsNotFound()
    {
        _repo
            .Setup(x => x.GameExistsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.GetPagedAsync(1, 1, 10, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Game not found.", result.Message);

        _repo.Verify(x => x.GetPagedAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPagedAsync_WhenPageAndPageSizeAreInvalid_NormalizesValues()
    {
        _repo
            .Setup(x => x.GameExistsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repo
            .Setup(x => x.GetPagedAsync(1, 1, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new List<ItemRarity>
                {
                    new() { ID = 1, GameId = 1, RarityName = "Common" }
                },
                1
            ));

        var result = await _service.GetPagedAsync(1, 0, 0, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data!.Page);
        Assert.Equal(10, result.Data.PageSize);
        Assert.Equal(1, result.Data.TotalCount);
        Assert.Equal(1, result.Data.TotalPages);
        Assert.Single(result.Data.Elements);
        Assert.Equal(1, result.Data.Elements[0].Id);
        Assert.Equal("Common", result.Data.Elements[0].Name);
    }

    [Fact]
    public async Task GetPagedAsync_WhenPageSizeIsGreaterThan50_LimitsPageSizeTo50()
    {
        _repo
            .Setup(x => x.GameExistsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repo
            .Setup(x => x.GetPagedAsync(1, 2, 50, "r", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ItemRarity>(), 0));

        var result = await _service.GetPagedAsync(1, 2, 999, "r", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Page);
        Assert.Equal(50, result.Data.PageSize);

        _repo.Verify(x => x.GetPagedAsync(
            1,
            2,
            50,
            "r",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_WhenDataExists_ReturnsMappedPagedResponse()
    {
        _repo
            .Setup(x => x.GameExistsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repo
            .Setup(x => x.GetPagedAsync(1, 2, 2, "r", It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new List<ItemRarity>
                {
                    new() { ID = 3, GameId = 1, RarityName = "Rare" },
                    new() { ID = 4, GameId = 1, RarityName = "Epic" }
                },
                5
            ));

        var result = await _service.GetPagedAsync(1, 2, 2, "r", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Page);
        Assert.Equal(2, result.Data.PageSize);
        Assert.Equal(5, result.Data.TotalCount);
        Assert.Equal(3, result.Data.TotalPages);
        Assert.Equal(2, result.Data.Elements.Count);
        Assert.Contains(result.Data.Elements, x => x.Id == 3 && x.Name == "Rare");
        Assert.Contains(result.Data.Elements, x => x.Id == 4 && x.Name == "Epic");
    }

    [Fact]
    public async Task CreateAsync_WhenGameIdIsInvalid_ReturnsBadRequest()
    {
        var req = new CreateItemRarityRequest(0, "Rare");

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("GameId is required.", result.Message);

        _repo.Verify(x => x.GameExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(x => x.AddAsync(It.IsAny<ItemRarity>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenRarityNameIsEmpty_ReturnsBadRequest()
    {
        var req = new CreateItemRarityRequest(1, "   ");

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("RarityName is required.", result.Message);

        _repo.Verify(x => x.GameExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(x => x.AddAsync(It.IsAny<ItemRarity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenRarityNameIsLongerThan20_ReturnsBadRequest()
    {
        var req = new CreateItemRarityRequest(1, "123456789012345678901");

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("RarityName max 20 chars.", result.Message);

        _repo.Verify(x => x.GameExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenGameDoesNotExist_ReturnsNotFound()
    {
        var req = new CreateItemRarityRequest(1, "Rare");

        _repo
            .Setup(x => x.GameExistsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Game not found.", result.Message);

        _repo.Verify(x => x.ExistsActiveByNameAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _repo.Verify(x => x.AddAsync(It.IsAny<ItemRarity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenRarityAlreadyExistsForGame_ReturnsConflict()
    {
        var req = new CreateItemRarityRequest(1, "Rare");

        _repo
            .Setup(x => x.GameExistsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repo
            .Setup(x => x.ExistsActiveByNameAsync(1, "Rare", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Rarity name already exists for this game.", result.Message);

        _repo.Verify(x => x.AddAsync(It.IsAny<ItemRarity>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenValidRequest_TrimsNameAddsEntityAndSaves()
    {
        var req = new CreateItemRarityRequest(1, "  Rare  ");

        _repo
            .Setup(x => x.GameExistsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repo
            .Setup(x => x.ExistsActiveByNameAsync(1, "Rare", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repo
            .Setup(x => x.AddAsync(It.IsAny<ItemRarity>(), It.IsAny<CancellationToken>()))
            .Callback<ItemRarity, CancellationToken>((entity, _) => entity.ID = 123)
            .Returns(Task.CompletedTask);

        var result = await _service.CreateAsync(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(123, result.Data);

        _repo.Verify(x => x.AddAsync(
            It.Is<ItemRarity>(r =>
                r.GameId == 1 &&
                r.RarityName == "Rare" &&
                !r.IsDeleted),
            It.IsAny<CancellationToken>()), Times.Once);

        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateNameAsync_WhenIdIsInvalid_ReturnsBadRequest()
    {
        var req = new UpdateItemRarityRequest("Rare");

        var result = await _service.UpdateNameAsync(0, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Id is required.", result.Message);

        _repo.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateNameAsync_WhenNameIsEmpty_ReturnsBadRequest()
    {
        var req = new UpdateItemRarityRequest("   ");

        var result = await _service.UpdateNameAsync(1, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("RarityName is required.", result.Message);

        _repo.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateNameAsync_WhenNameIsLongerThan20_ReturnsBadRequest()
    {
        var req = new UpdateItemRarityRequest("123456789012345678901");

        var result = await _service.UpdateNameAsync(1, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("RarityName max 20 chars.", result.Message);

        _repo.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateNameAsync_WhenRarityDoesNotExist_ReturnsNotFound()
    {
        var req = new UpdateItemRarityRequest("Rare");

        _repo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ItemRarity?)null);

        var result = await _service.UpdateNameAsync(1, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Item rarity not found.", result.Message);

        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateNameAsync_WhenRarityIsDeleted_ReturnsNotFound()
    {
        var req = new UpdateItemRarityRequest("Rare");

        _repo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ItemRarity
            {
                ID = 1,
                GameId = 1,
                RarityName = "Common",
                IsDeleted = true
            });

        var result = await _service.UpdateNameAsync(1, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Item rarity not found.", result.Message);

        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateNameAsync_WhenNameIsTheSame_ReturnsNoContentAndDoesNotSave()
    {
        var req = new UpdateItemRarityRequest("Rare");

        _repo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ItemRarity
            {
                ID = 1,
                GameId = 1,
                RarityName = "Rare",
                IsDeleted = false
            });

        var result = await _service.UpdateNameAsync(1, req, CancellationToken.None);

        Assert.True(result.IsSuccess);

        _repo.Verify(x => x.ExistsActiveByNameAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateNameAsync_WhenNameAlreadyExistsForGame_ReturnsConflict()
    {
        var req = new UpdateItemRarityRequest("Epic");

        _repo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ItemRarity
            {
                ID = 1,
                GameId = 10,
                RarityName = "Rare",
                IsDeleted = false
            });

        _repo
            .Setup(x => x.ExistsActiveByNameAsync(10, "Epic", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.UpdateNameAsync(1, req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Rarity name already exists for this game.", result.Message);

        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateNameAsync_WhenValidRequest_UpdatesNameAndSaves()
    {
        var req = new UpdateItemRarityRequest("  Epic  ");

        var entity = new ItemRarity
        {
            ID = 1,
            GameId = 10,
            RarityName = "Rare",
            IsDeleted = false
        };

        _repo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        _repo
            .Setup(x => x.ExistsActiveByNameAsync(10, "Epic", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.UpdateNameAsync(1, req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated.", result.Message);
        Assert.Equal("Epic", entity.RarityName);

        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SoftDeleteAsync_WhenIdIsInvalid_ReturnsBadRequest()
    {
        var result = await _service.SoftDeleteAsync(0, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Id is required.", result.Message);

        _repo.Verify(x => x.GetByIdWithNoTrackAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(x => x.SoftDeleteCascadeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteAsync_WhenRarityDoesNotExist_ReturnsNotFound()
    {
        _repo
            .Setup(x => x.GetByIdWithNoTrackAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ItemRarity?)null);

        var result = await _service.SoftDeleteAsync(1, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("No ItemRarity for provided id has been found.", result.Message);

        _repo.Verify(x => x.SoftDeleteCascadeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteAsync_WhenRarityExists_CallsSoftDeleteCascade()
    {
        _repo
            .Setup(x => x.GetByIdWithNoTrackAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ItemRarity
            {
                ID = 1,
                GameId = 1,
                RarityName = "Rare",
                IsDeleted = false
            });

        var result = await _service.SoftDeleteAsync(1, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Deleted.", result.Message);

        _repo.Verify(x => x.SoftDeleteCascadeAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }
}