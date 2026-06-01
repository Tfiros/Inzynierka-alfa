using ItemTradeApp.Features.Favourites;
using ItemTradeApp.Features.Favourites.Repositories;
using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using JetBrains.Annotations;
using Moq;

namespace ItemTradeApp.UnitTests.Features.Favourites;

[TestSubject(typeof(FavouritesService))]
public class FavouritesServiceTest
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IFavouritesRepository> _favouritesRepo = new();
    private readonly Mock<IOffersRepository> _offersRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private readonly FavouritesService _favouritesService;

    public FavouritesServiceTest()
    {
        _favouritesService = new FavouritesService(_userRepo.Object, _favouritesRepo.Object, _offersRepo.Object, _uow.Object);
    }
    
    [Fact]
    public async Task GetFavouritesAsync_WhenAuth0IdMissing_ReturnsUnauthorized()
    {
        var result = await _favouritesService.GetFavouriteIdsAsync(null);
        Assert.False(result.IsSuccess);
        Assert.Equal("missing_sub_claim", result.Message);
    }

    [Fact]
    public async Task GetFavouritesAsync_WhenPageInvalid_ReturnsBadRequest()
    {
        var result = await _favouritesService.GetFavouritesAsync("auth0|abc", 0, 100);
        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_page_number", result.Message);
    }
    
    [Fact]
    public async Task GetFavouritesAsync_WhenPageSizeInvalid_ReturnsBadRequest()
    {
        var result = await _favouritesService.GetFavouritesAsync("auth0|abc", 1, -1);
        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_page_size", result.Message);
    }
    
    [Fact]
    public async Task GetFavouritesAsync_WhenUserNotFound_ReturnsUnauthorized()
    {
        _userRepo.Setup(x => x.GetUserIdByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
        var result = await _favouritesService.GetFavouritesAsync("auth0|abc", 1, 1);
        Assert.False(result.IsSuccess);
        Assert.Equal("user_not_found", result.Message);
    }
    
    [Fact]
    public async Task GetFavouritesAsync_WhenPageSizeIsGreaterThan100_LimitsPageSizeTo100()
    {

        var elements = new List<OfferListingDTO>();
        
        _userRepo.Setup(x => x.GetUserIdByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        _favouritesRepo.Setup(x => x.GetFavouriteOffersPagedAsync(42, 2, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((elements, 250));
        
        var result = await _favouritesService.GetFavouritesAsync("auth0|abc", 2, 999);
        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Data!.PageSize);
        Assert.Equal(250, result.Data!.TotalCount);
        Assert.Equal(3, result.Data!.TotalPages);
        Assert.Same(elements, result.Data.Elements);
        _favouritesRepo.Verify(x => x.GetFavouriteOffersPagedAsync(42, 2, 100, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GetFavouriteIds_WhenUserNotFound_ReturnsUnauthorized()
    {
        _userRepo.Setup(x => x.GetUserIdByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
        
        var result = await _favouritesService.GetFavouriteIdsAsync("auth0|abc");
        Assert.False(result.IsSuccess);
        Assert.Equal("user_not_found", result.Message);
    }
    
    [Fact]
    public async Task GetFavouriteIds_WhenAuth0IdMissing_ReturnsUnauthorized()
    {
        var result = await _favouritesService.GetFavouriteIdsAsync(null);
        Assert.False(result.IsSuccess);
        Assert.Equal("missing_sub_claim", result.Message);
    }
    
    [Fact]
    public async Task GetFavouriteIds_WhenFavouriteExists_ReturnsIds()
    {
        var ids = new List<int> { 1, 2, 3 };
        
        _userRepo.Setup(x => x.GetUserIdByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(67);

        _favouritesRepo.Setup(x => x.GetFavouriteIdsAsync(67, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);
        
        var result = await _favouritesService.GetFavouriteIdsAsync("auth0|abc");
        Assert.True(result.IsSuccess);
        Assert.Same(ids, result.Data);
        _favouritesRepo.Verify(x => x.GetFavouriteIdsAsync(67, It.IsAny<CancellationToken>()), Times.Once);

    }
    
    
    [Fact]
    public async Task AddFavourite_WhenAuth0IdMissing_ReturnsUnauthorized()
    {
        var result = await _favouritesService.AddFavourite(null, 7);
        Assert.False(result.IsSuccess);
        Assert.Equal("missing_sub_claim", result.Message);
    }
    
    
    [Fact]
    public async Task AddFavourite_WhenOfferIdInvalid_ReturnsBadRequest()
    {
        var result = await _favouritesService.AddFavourite("auth0|abc", 0);
        Assert.False(result.IsSuccess);
        Assert.Equal("incorrect_offer_id", result.Message);
    }
    
    [Fact]
    public async Task AddFavourite_WhenUserNotFound_ReturnsUnauthorized()
    {
        _userRepo.Setup(x => x.GetUserIdByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
        
        var result = await _favouritesService.AddFavourite("auth0|abc", 7);
        Assert.False(result.IsSuccess);
        Assert.Equal("user_not_found", result.Message);
    }
    
    
    [Fact]
    public async Task AddFavourite_WhenOfferNotActive_ReturnsBadRequest()
    {
        _userRepo.Setup(x => x.GetUserIdByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        
        _offersRepo.Setup(x => x.OfferIsActiveAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        var result = await _favouritesService.AddFavourite("auth0|abc", 7);
        Assert.False(result.IsSuccess);
        Assert.Equal("offer_not_active", result.Message);
        _favouritesRepo.Verify(x => x.Add(It.IsAny<UserFavouriteOffer>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task AddFavourite_WhenOfferAlreadyFavourited_ReturnsSuccessWithoutSaving()
    {
        _userRepo.Setup(x => x.GetUserIdByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        
        _offersRepo.Setup(x => x.OfferIsActiveAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _favouritesRepo.Setup(x => x.FavouriteExistsAsync(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        var result = await _favouritesService.AddFavourite("auth0|abc", 7);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
        _favouritesRepo.Verify(x => x.Add(It.IsAny<UserFavouriteOffer>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task AddFavourite_WhenValidAndOfferNew_ReturnsSuccessWithSaving()
    {
        _userRepo.Setup(x => x.GetUserIdByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        
        _offersRepo.Setup(x => x.OfferIsActiveAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _favouritesRepo.Setup(x => x.FavouriteExistsAsync(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        var result = await _favouritesService.AddFavourite("auth0|abc", 7);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
        _favouritesRepo.Verify(x => x.Add(It.Is<UserFavouriteOffer>(f => f.User_ID == 42 && f.Offer_ID ==  7)), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task AddFavourite_WhenSaveFails_ReturnsInternalServerError()
    {
        _userRepo.Setup(x => x.GetUserIdByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        
        _offersRepo.Setup(x => x.OfferIsActiveAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _favouritesRepo.Setup(x => x.FavouriteExistsAsync(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());
        
        var result = await _favouritesService.AddFavourite("auth0|abc", 7);
        Assert.False(result.IsSuccess);
        Assert.Equal("add_favourite_failed",result.Message);
        _favouritesRepo.Verify(x => x.Add(It.Is<UserFavouriteOffer>(f => f.User_ID == 42 && f.Offer_ID ==  7)), Times.Once);
    }

    
    [Fact]
    public async Task RemoveFavourite_WhenAuth0IdMissing_ReturnsUnauthorized()
    {
        var result = await _favouritesService.RemoveFavourite(null, 7);
        Assert.False(result.IsSuccess);
        Assert.Equal("missing_sub_claim", result.Message);
    }
    
    [Fact]
    public async Task RemoveFavourite_WhenUserNotFound_ReturnsUnauthorized()
    {
        _userRepo.Setup(x => x.GetUserIdByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
        
        var result = await _favouritesService.RemoveFavourite("auth0|abc", 7);
        Assert.False(result.IsSuccess);
        Assert.Equal("user_not_found", result.Message);
    }
    
    [Fact]
    public async Task RemoveFavourite_WhenOfferIdInvalid_ReturnsBadRequest()
    {
        var result = await _favouritesService.RemoveFavourite("auth0|abc", 0);
        Assert.False(result.IsSuccess);
        Assert.Equal("incorrect_offer_id", result.Message);
    }
    
    [Fact]
    public async Task RemoveFavourite_WhenOfferNotFound_ReturnsBadRequest()
    {
        _userRepo.Setup(x => x.GetUserIdByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        
        _offersRepo.Setup(x => x.OfferExistsAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        var result = await _favouritesService.RemoveFavourite("auth0|abc", 7);
        Assert.False(result.IsSuccess);
        Assert.Equal("offer_not_found",result.Message);
        _favouritesRepo.Verify(x => x.RemoveAsync( It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task RemoveFavourite_WhenValid_RemovesAndReturnsSuccess()
    {
        _userRepo.Setup(x => x.GetUserIdByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        
        _offersRepo.Setup(x => x.OfferExistsAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _favouritesRepo.Setup(x => x.RemoveAsync(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        var result = await _favouritesService.RemoveFavourite("auth0|abc", 7);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
        _favouritesRepo.Verify(x => x.RemoveAsync(42, 7, It.IsAny<CancellationToken>()), Times.Once);
    }

    
}