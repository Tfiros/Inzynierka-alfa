using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;
using ItemTradeApp.Features.Shared.Images;
using ItemTradeApp.Features.Users.UserInfo;
using ItemTradeApp.Features.Users.UserInfo.DTOs.Request;
using ItemTradeApp.Features.Users.UserInfo.DTOs.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;

namespace ItemTradeApp.UnitTests.Features.Users.UserInfo;

public sealed class UserInfoOfferServiceTests
{
    private readonly Mock<IUserInfoOfferRepository> _offerRepo = new();
    private readonly Mock<IUserInfoRepository> _userRepo = new();

    private UserInfoOfferService CreateService() => new(_offerRepo.Object, _userRepo.Object);

    [Fact]
    public async Task GetPagedActiveAsync_ShouldReturnBadRequest_WhenPageIsInvalid()
    {
        var service = CreateService();

        var result = await service.GetPagedActiveAsync(1, 0, 10);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_page_number", result.Message);
    }

    [Fact]
    public async Task GetPagedActiveAsync_ShouldReturnBadRequest_WhenPageSizeIsInvalid()
    {
        var service = CreateService();

        var result = await service.GetPagedActiveAsync(1, 1, 0);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_page_size", result.Message);
    }

    [Fact]
    public async Task GetPagedActiveAsync_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        _userRepo.Setup(x => x.ExistsByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        var result = await service.GetPagedActiveAsync(5, 1, 10);

        Assert.False(result.IsSuccess);
        Assert.Equal("user_not_found", result.Message);

        _offerRepo.Verify(
            x => x.GetActiveForUserByIdPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPagedActiveAsync_ShouldReturnPagedResponse_WhenUserExists()
    {
        var offers = new List<OfferListingDTO>();

        _userRepo.Setup(x => x.ExistsByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _offerRepo.Setup(x => x.GetActiveForUserByIdPagedAsync(5, 2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((offers, 25));

        var service = CreateService();

        var result = await service.GetPagedActiveAsync(5, 2, 10);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Page);
        Assert.Equal(10, result.Data.PageSize);
        Assert.Equal(25, result.Data.TotalCount);
        Assert.Equal(3, result.Data.TotalPages);
        Assert.Same(offers, result.Data.Elements);
    }

    [Fact]
    public async Task GetPagedActiveAsync_ShouldClampPageSizeTo100()
    {
        _userRepo.Setup(x => x.ExistsByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _offerRepo.Setup(x => x.GetActiveForUserByIdPagedAsync(5, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OfferListingDTO>(), 250));

        var service = CreateService();

        var result = await service.GetPagedActiveAsync(5, 1, 999);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Data!.PageSize);

        _offerRepo.Verify(x => x.GetActiveForUserByIdPagedAsync(5, 1, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPagedHistoryAsync_ShouldReturnBadRequest_WhenPageIsInvalid()
    {
        var service = CreateService();

        var result = await service.GetPagedHistoryAsync(1, 0, 10);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_page_number", result.Message);
    }

    [Fact]
    public async Task GetPagedHistoryAsync_ShouldReturnBadRequest_WhenPageSizeIsInvalid()
    {
        var service = CreateService();

        var result = await service.GetPagedHistoryAsync(1, 1, 0);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_page_size", result.Message);
    }

    [Fact]
    public async Task GetPagedHistoryAsync_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        _userRepo.Setup(x => x.ExistsByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        var result = await service.GetPagedHistoryAsync(5, 1, 10);

        Assert.False(result.IsSuccess);
        Assert.Equal("user_not_found", result.Message);

        _offerRepo.Verify(
            x => x.GetHistoryForUserByIdPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPagedHistoryAsync_ShouldReturnPagedResponse_WhenUserExists()
    {
        var offers = new List<OfferListingDTO>();

        _userRepo.Setup(x => x.ExistsByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _offerRepo.Setup(x => x.GetHistoryForUserByIdPagedAsync(5, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((offers, 41));

        var service = CreateService();

        var result = await service.GetPagedHistoryAsync(5, 1, 20);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(20, result.Data.PageSize);
        Assert.Equal(41, result.Data.TotalCount);
        Assert.Equal(3, result.Data.TotalPages);
        Assert.Same(offers, result.Data.Elements);
    }
}