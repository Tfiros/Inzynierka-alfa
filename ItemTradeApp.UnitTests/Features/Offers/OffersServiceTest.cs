using ItemTradeApp.Features.Offers;
using ItemTradeApp.Features.Offers.DTOs.RequestDTOs;
using ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;
using ItemTradeApp.Features.Offers.Repositories;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;
using ItemTradeApp.Features.Shared.Emails.Services;
using ItemTradeApp.Features.Shared.Notifications;
using ItemTradeApp.Features.Shared.TokenEscrow;
using ItemTradeApp.Features.Shared.TradeCreation;
using ItemTradeApp.Features.Shared.TradeCreation.DTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using ItemTradeApp.Resources.NotificationsTemplates;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace ItemTradeApp.UnitTests.Features.Offers;

[TestSubject(typeof(OffersService))]
public class OffersServiceTest
{
    private readonly Mock<IOffersRepository> _offersRepo = new();
    private readonly Mock<IUsersRepository> _userRepo = new();
    private readonly Mock<IItemsRepository> _itemsRepo = new();
    private readonly Mock<IGamesRepository> _gamesRepo = new();
    private readonly Mock<IGenresRepository> _genresRepo = new();
    private readonly Mock<IRaritiesRepository> _raritiesRepo = new();
    private readonly Mock<ITradeRepository> _tradesRepo = new();
    private readonly Mock<ICounterOfferRepository> _counterOffersRepo = new();
    private readonly Mock<ITradeCreation> _tradeCreation = new();
    private readonly Mock<ITokenEscrow> _tokenEscrow = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<INotificationSender> _notificationSender = new();
    private readonly Mock<IEmailGenerationService> _emailService = new();

    private readonly OffersService _offersService;


    public OffersServiceTest()
    {
        _offersService = new OffersService(
            _offersRepo.Object, 
            _userRepo.Object, 
            _itemsRepo.Object, 
            _gamesRepo.Object, 
            _genresRepo.Object, 
            _raritiesRepo.Object,
            _tradesRepo.Object, 
            _counterOffersRepo.Object, 
            _tradeCreation.Object, 
            _tokenEscrow.Object, 
            _uow.Object, 
            _notificationSender.Object, 
            _emailService.Object);
    }

    private static Item CreateItem(int id, int gameId, string name, int value)
        => new Item()
        {
            ID = id,
            Game_ID = gameId,
            Name = name,
            Photo_URL = $"item-photo-{id}",
            EstimatedTokenValue = value,
            Game = new Game()
            {
                ID = gameId,
                Name = $"game-{gameId}",
                Photo_URL = $"game-photo-{gameId}",
                Genre_ID = 9
            }
        };

    private static UserNotificationData Buyer() => new ( 1, "buyer@test.com", "buyer");
    private static UserNotificationData Seller() => new ( 2, "seller@test.com", "seller");
    
    private void SetupValidItems()
        => _offersRepo.Setup(x => x.GetItemsByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new Dictionary<int, Item>
                {
                    [1] = CreateItem(1, 3, "Offered", 1000),
                    [2] = CreateItem(2, 3, "Wanted", 1000)
                }
            );

    private static OfferDraftRequest ValidCreateRequest(int tokensOffered = 0, int tokensWanted = 0)
        => new OfferDraftRequest()
        {
            Title = "Title",
            Description = "Description",
            DurationDays = 7,
            IsHighlighted = false,
            TokensOffered = tokensOffered,
            TokensWanted = tokensWanted,
            OfferedItems = new List<OfferItemDTO> { new(1, 1) },
            WantedItems = new List<OfferItemDTO> { new(2, 1) }

        };
    
    private static OfferUpdateDraftRequest ValidUpdateRequest(int durationDays = 0, int tokensOffered = 0,
        int tokensWanted = 0)
        => new OfferUpdateDraftRequest
        {
            Title = "Title",
            Description = "Description",
            DurationDays = durationDays,
            IsHighlighted = false,
            TokensOffered = tokensOffered,
            TokensWanted = tokensWanted,
            OfferedItems = new List<OfferItemDTO>{new(1,1)},
            WantedItems = new List<OfferItemDTO>{new(2,1)}
        };

    private static Offer ActiveOffer(int tokenCost = 20, int tokensOffered = 20)
        => new Offer
        {
            ID = 7,
            OfferStatus_ID = (int)OfferStatuses.Active,
            TokenCost = tokenCost,
            TokensOffered = tokensOffered,
            TokensWanted = 0,
            ExpDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),
            ListingItems = new List<ListingItems>
            {
                new ListingItems { Item_ID = 1, IsWanted = false, Quantity = 1 },
                new ListingItems { Item_ID = 2, IsWanted = true, Quantity = 1 },
            }
        };

    private static OfferDetailsDTO UpdatedDetails()
        => new OfferDetailsDTO(
            new OfferCoreDTO(7, "Title", "Description", DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow, 40, 1,
                false, 0, 0),
            new OfferUserDTO(1, "Nickname", null, 0, 0f, 0f),
            new List<OfferListingItemDTO>(),
            new List<OfferListingItemDTO>()
        );
    
    //GetQuoteAsync
    [Fact]
    public async Task GetQuoteAsync_WhenTitleInvalid_ReturnsBadRequest()
    {
        var req = new OfferDraftRequest
        {
            Title = "",
            Description = "Description",
            DurationDays = 7,
            OfferedItems = new List<OfferItemDTO>{new(1,1)},
            WantedItems = new List<OfferItemDTO>{new(1,1)},
        };

        var res = await _offersService.GetQuoteAsync(req);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("title_required", res.Message);
        _offersRepo.Verify(x => x.GetItemsByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task GetQuoteAsync_WhenDescriptionEmpty_ReturnsBadRequest()
    {
        var req = new OfferDraftRequest
        {
            Title = "Title",
            Description = "",
            DurationDays = 7,
            OfferedItems = new List<OfferItemDTO>{new(1,1)},
            WantedItems = new List<OfferItemDTO>{new(1,1)},
        };

        var res = await _offersService.GetQuoteAsync(req);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("description_required", res.Message);
        _offersRepo.Verify(x => x.GetItemsByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task GetQuoteAsync_WhenDescriptionIsTooShort_ReturnsBadRequest()
    {
        var req = new OfferDraftRequest
        {
            Title = "Title",
            Description = "ab",
            DurationDays = 7,
            OfferedItems = new List<OfferItemDTO>{new(1,1)},
            WantedItems = new List<OfferItemDTO>{new(1,1)},
        };

        var res = await _offersService.GetQuoteAsync(req);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("description_required", res.Message);
        _offersRepo.Verify(x => x.GetItemsByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task GetQuoteAsync_WhenTokensInvolved_AddsTokensContributionToCost()
    {
        var req = new OfferDraftRequest
        {
            Title = "Title",
            Description = "Description",
            DurationDays = 7,
            TokensOffered = 0,
            TokensWanted = 500,
            OfferedItems = new List<OfferItemDTO>{new(1,1)},
            WantedItems = new List<OfferItemDTO>(),
        };

        _offersRepo.Setup(
            x => x.GetItemsByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, Item>
            {
                [1] = CreateItem(1, 3, "Offered", 1000)
            });
        
        var res = await _offersService.GetQuoteAsync(req);
        
        Assert.True(res.IsSuccess);
        Assert.Equal(30, res.Data!.FinalCost);
    }
    
    [Theory]
    [InlineData(100, 100, 7, false, 10)]
    [InlineData(1000, 1000, 7, false, 40)]
    [InlineData(1000, 1000, 14, false, 70)]
    [InlineData(1000, 1000, 31, true, 150)]
    public async Task GetQuoteAsync_WhenValid_CalculatesFinalCost(
        int offeredValue, int wantedValue, int durationDays, bool isHighlighted, int expected
        )
    {
        var req = new OfferDraftRequest
        {
            Title = "Title",
            Description = "Description",
            DurationDays = durationDays,
            IsHighlighted = isHighlighted,
            TokensOffered = 0,
            TokensWanted = 0,
            OfferedItems = new List<OfferItemDTO>{new(1,1)},
            WantedItems = new List<OfferItemDTO>{new(2, 1)},
        };

        _offersRepo.Setup(
                x => x.GetItemsByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, Item>
            {
                [1] = CreateItem(1, 3, "Offered", offeredValue),
                [2] = CreateItem(2, 3, "Wanted", wantedValue)
            });
        
        var res = await _offersService.GetQuoteAsync(req);
        
        Assert.True(res.IsSuccess);
        Assert.Equal(expected, res.Data!.FinalCost);
    }

    [Fact]
    public async Task GetQuoteAsync_WhenItemDoesNotExist_ReturnsBadRequest()
    {
        var req = new OfferDraftRequest
        {
            Title = "Title",
            Description = "Description",
            DurationDays = 7,
            OfferedItems = new List<OfferItemDTO> { new(1, 1) },
            WantedItems = new List<OfferItemDTO> { new(2, 1) }
        };

        _offersRepo.Setup(
            x => x.GetItemsByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, Item>
            {
                [1] = CreateItem(1, 3, "Offered", 1000),
            });

        var res = await _offersService.GetQuoteAsync(req);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("items_not_found: 2", res.Message);
    }

    [Fact]
    public async Task GetQuoteAsync_WhenNoItemsOnEitherSide_ReturnsBadRequest()
    {
        var req = new OfferDraftRequest
        {
            Title = "Title",
            Description = "Description",
            DurationDays = 7,
            OfferedItems = new List<OfferItemDTO>(),
            WantedItems = new List<OfferItemDTO>()
        };

        var res = await _offersService.GetQuoteAsync(req);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("at_least_one_side_must_have_items", res.Message);
        _offersRepo.Verify(x => x.GetItemsByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task GetQuoteAsync_WhenNoItemsOfferedAndNoTokensOffered_ReturnsBadRequest()
    {
        var req = new OfferDraftRequest
        {
            Title = "Title",
            Description = "Description",
            DurationDays = 7,
            TokensOffered = 0,
            TokensWanted = 0,
            OfferedItems = new List<OfferItemDTO>(),
            WantedItems = new List<OfferItemDTO>{new(2,1)}
        };

        var res = await _offersService.GetQuoteAsync(req);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("must_offer_tokens_when_no_items_offered", res.Message);
        _offersRepo.Verify(x => x.GetItemsByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task GetQuoteAsync_WhenNoItemsWantedAndNoTokensWanted_ReturnsBadRequest()
    {
        var req = new OfferDraftRequest
        {
            Title = "Title",
            Description = "Description",
            DurationDays = 7,
            TokensOffered = 0,
            TokensWanted = 0,
            OfferedItems = new List<OfferItemDTO>{new(2,1)},
            WantedItems = new List<OfferItemDTO>()
        };

        var res = await _offersService.GetQuoteAsync(req);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("must_want_tokens_when_no_items_wanted", res.Message);
        _offersRepo.Verify(x => x.GetItemsByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task GetQuoteAsync_WhenDurationDaysInvalid_ReturnsBadRequest()
    {
        var req = new OfferDraftRequest
        {
            Title = "Title",
            Description = "Description",
            DurationDays = 5,
            OfferedItems = new List<OfferItemDTO>{new(2,1)},
            WantedItems = new List<OfferItemDTO>{new(1,2)}
        };

        var res = await _offersService.GetQuoteAsync(req);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("invalid_duration_days", res.Message);
        _offersRepo.Verify(x => x.GetItemsByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    //GetItemsByName
    [Fact]
    public async Task GetItemsByName_WhenTextWhiteSpace_ReturnsEmptyList()
    {
        var res = await _offersService.GetItemsByName("  ");
        Assert.True(res.IsSuccess);
        Assert.Empty(res.Data!);
        _itemsRepo.Verify(x => x.GetByName(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetItemsByName_WhenFound_ReturnsList()
    {
        _itemsRepo.Setup(x => x.GetByName("swo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Item>
            {
                CreateItem(5, 3, "Sword", 250)
            });

        var res = await _offersService.GetItemsByName("swo");
        
        Assert.True(res.IsSuccess);
        var dto = Assert.Single(res.Data!);
        Assert.Equal(5, dto.Id);
        Assert.Equal("Sword", dto.Name);
        Assert.Equal("item-photo-5", dto.PhotoUrl);
        Assert.Equal(250, dto.EstimatedTokenValue);
        Assert.Equal(3, dto.Game.Id);
        Assert.Equal("game-3", dto.Game.Name);
        Assert.Equal("game-photo-3", dto.Game.PhotoUrl);
        Assert.Equal(9, dto.Game.GenreId);
    }

    //GetItemsByNameAndGameId

    [Fact]
    public async Task GetItemsByNameAndGameId_WhenSearchTextIsWhiteSpace_ReturnsEmptyList()
    {
        var res = await _offersService.GetItemsByNameAndGameId("  ", 12);
        Assert.True(res.IsSuccess);
        Assert.Empty(res.Data!);
        _itemsRepo.Verify(x => x.GetByNameAndGameId(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task GetItemsByNameAndGameId_WhenGameIdInvalid_ReturnsBadRequest()
    {
        var res = await _offersService.GetItemsByNameAndGameId("swor", 0);
        Assert.False(res.IsSuccess);
        Assert.Equal("Game ID is required",res.Message);
        _itemsRepo.Verify(x => x.GetByNameAndGameId(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task GetItemsByNameAndGameId_WhenFound_ReturnsList()
    {
        _itemsRepo.Setup(x => x.GetByNameAndGameId("swo", 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Item>
            {
                CreateItem(5, 3, "Sword", 250)
            });
        
        var res = await _offersService.GetItemsByNameAndGameId("swo", 3);
        Assert.True(res.IsSuccess);
        var dto = Assert.Single(res.Data!);
        Assert.Equal(5, dto.Id);
        Assert.Equal(3, dto.Game.Id);
        Assert.Equal("Sword", dto.Name);
        Assert.Equal(250, dto.EstimatedTokenValue);
    }
    
    
    //GetAllGames
    [Fact]
    public async Task GetAllGames_ReturnsList()
    {
        _gamesRepo.Setup(x => x.GetAll(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Game>
                {
                    new Game() { ID = 3, Name = "Elden", Photo_URL = "game-url", Genre_ID = 9 }
                }
            );
        var res = await _offersService.GetAllGames();
        
        Assert.True(res.IsSuccess);
        var dto = Assert.Single(res.Data!);
        Assert.Equal(3, dto.Id);
        Assert.Equal("Elden", dto.Name);
        Assert.Equal("game-url", dto.PhotoUrl);
        Assert.Equal(9, dto.GenreId);
    }
    
    //GetAllGenres
    [Fact]
    public async Task GetAllGenres_ReturnsList()
    {
        _genresRepo.Setup(x => x.GetAll(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Genre>
                {
                    new Genre() { ID = 3, Name = "RPG" }
                }
            );
        var res = await _offersService.GetAllGenres();
        
        Assert.True(res.IsSuccess);
        var dto = Assert.Single(res.Data!);
        Assert.Equal(3, dto.Id);
        Assert.Equal("RPG", dto.Name);
    }
    
    //GetRaritiesByGameId
    [Fact]
    public async Task GetRaritiesByGameId_WhenGameIdInvalid_ReturnBadRequest()
    {
        var res = await _offersService.GetRaritiesByGameId(0);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("invalid_game_id", res.Message);
        _raritiesRepo.Verify(x => x.GetByGameId(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task GetRaritiesByGameId_WhenValid_ReturnList()
    {
        _raritiesRepo.Setup(x => x.GetByGameId(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ItemRarity>
                {
                    new ItemRarity {ID = 2, RarityName = "Legendary", GameId = 3}
                });
        
        var res = await _offersService.GetRaritiesByGameId(3);
        
        Assert.True(res.IsSuccess);
        var dto = Assert.Single(res.Data!);
        Assert.Equal(2, dto.Id);
        Assert.Equal("Legendary", dto.Name);
    }
    
    //GetOffersAsync
    [Fact]
    public async Task GetOffersAsync_WhenPageInvalid_ReturnBadRequest()
    {
        var res = await _offersService.GetOffersAsync(new OfferListingsQuery{Page = 0, PageSize = 20});
        
        Assert.False(res.IsSuccess);
        Assert.Equal("invalid_page_number", res.Message);
        _offersRepo.Verify(x => x.GetOffersPagedAsync(It.IsAny<OfferListingsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOffersAsync_WhenPageSizeInvalid_ReturnBadRequest()
    {
        var res = await _offersService.GetOffersAsync(new OfferListingsQuery{Page = 1, PageSize = 0});
        
        Assert.False(res.IsSuccess);
        Assert.Equal("invalid_page_size", res.Message);
        _offersRepo.Verify(x => x.GetOffersPagedAsync(It.IsAny<OfferListingsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task GetOffersAsync_WhenPageSizeExceeds100_CorrectTo100()
    {
        _offersRepo.Setup(x => x.GetOffersPagedAsync(It.IsAny<OfferListingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OfferListingDTO>(), 250));
        var res = await _offersService.GetOffersAsync(new OfferListingsQuery{Page = 1, PageSize = 999});
        
        Assert.True(res.IsSuccess);
        Assert.Equal(100, res.Data!.PageSize);
        Assert.Equal(250, res.Data!.TotalCount);
        Assert.Equal(3, res.Data!.TotalPages);
        _offersRepo.Verify(x => x.GetOffersPagedAsync(It.IsAny<OfferListingsQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GetOffersAsync_WhenTotalCount0_Return1Page()
    {
        _offersRepo.Setup(x => x.GetOffersPagedAsync(It.IsAny<OfferListingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OfferListingDTO>(), 0));
        var res = await _offersService.GetOffersAsync(new OfferListingsQuery{Page = 1, PageSize = 20});
        
        Assert.True(res.IsSuccess);
        Assert.Equal(20, res.Data!.PageSize);
        Assert.Equal(0, res.Data!.TotalCount);
        Assert.Equal(1, res.Data!.TotalPages);
        _offersRepo.Verify(x => x.GetOffersPagedAsync(It.IsAny<OfferListingsQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOffersAsync_WhenValid_ReturnPagedResponse()
    {

        _offersRepo.Setup(x => x.GetOffersPagedAsync(It.IsAny<OfferListingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OfferListingDTO>(), 25));
        
        var res = await _offersService.GetOffersAsync(new OfferListingsQuery{Page = 1, PageSize = 20});

        Assert.True(res.IsSuccess);
        Assert.Equal(20, res.Data!.PageSize);
        Assert.Equal(25, res.Data!.TotalCount);
        Assert.Equal(2, res.Data!.TotalPages);
        _offersRepo.Verify(x => x.GetOffersPagedAsync(It.IsAny<OfferListingsQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        
    }

    [Theory]
    [InlineData(0, null, null, "incorrect_game_id")]
    [InlineData(null, 0, null, "incorrect_genre_id")]
    [InlineData(null, null, 0, "incorrect_rarity_id")]
    public async Task GetOffersAsync_WhenFilterIdInvalid_ReturnsBadRequest(
        int? gameId, int? genreId, int? rarityId, string expected)
    {
        var res = await _offersService.GetOffersAsync(
            new OfferListingsQuery
            {
                Page = 1, PageSize = 20, GameId = gameId, GenreId = genreId, RarityId = rarityId
            });
        Assert.False(res.IsSuccess);
        Assert.Equal(expected, res.Message);
        _offersRepo.Verify(x => x.GetOffersPagedAsync(It.IsAny<OfferListingsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOffersAsync_WhenFiltersValid_ContinuesToRepo()
    {
        var query = new OfferListingsQuery { Page = 1, PageSize = 20, GameId = 3, GenreId = 4, RarityId = 5 };
        _offersRepo.Setup(x => x.GetOffersPagedAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OfferListingDTO>(), 0));

        var res = await _offersService.GetOffersAsync(query);
        
        Assert.True(res.IsSuccess);
        _offersRepo.Verify(x => x.GetOffersPagedAsync(query, It.IsAny<CancellationToken>()), Times.Once);

    } 

    //GetOfferByIdAsync
    [Fact]
    public async Task GetOfferByIdAsync_WhenIdInvalid_ReturnsBadRequest()
    {
        var res = await _offersService.GetOfferByIdAsync(0);
        Assert.False(res.IsSuccess);
        Assert.Equal("invalid_offer_id",res.Message);
    }

    [Fact]
    public async Task GetOfferByIdAsync_WhenNotFound_ReturnsNotFound()
    {
        _offersRepo.Setup(x => x.GetOfferWithDetailsByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OfferDetailsDTO?)null);

        var res = await _offersService.GetOfferByIdAsync(7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("offer_not_found",res.Message);
    }

    [Fact]
    public async Task GetOfferByIdAsync_WhenFound_ReturnOffer()
    {
        var offerDetails = new OfferDetailsDTO(
            new OfferCoreDTO(3, "Title", "Desc", DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow, 100, 1, false,
                0, 0),
            new OfferUserDTO(1, "Nickname", null, 0, 0f, 0f),
            new List<OfferListingItemDTO>(),
            new List<OfferListingItemDTO>()
        );

        _offersRepo.Setup(x => x.GetOfferWithDetailsByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offerDetails);

        var res = await _offersService.GetOfferByIdAsync(3);
        Assert.True(res.IsSuccess);
        Assert.Same(offerDetails, res.Data!);
    }
    
    
    //GetUpdateQuoteAsync
    [Fact]
    public async Task GetUpdateQuoteAsync_WhenAuth0IdMissing_ReturnsUnauthorized()
    {
        var res = await _offersService.GetUpdateQuoteAsync(null, 7, new OfferUpdateDraftRequest());
        
        Assert.False(res.IsSuccess);
        Assert.Equal("missing_sub_claim",res.Message);
    }
    
    [Fact]
    public async Task GetUpdateQuoteAsync_WhenOfferIdInvalid_ReturnsBadRequest()
    {
        var res = await _offersService.GetUpdateQuoteAsync("auth0|abc", 0, new OfferUpdateDraftRequest());
        
        Assert.False(res.IsSuccess);
        Assert.Equal("invalid_offer_id",res.Message);
    }
    
    [Fact]
    public async Task GetUpdateQuoteAsync_WhenUserNotFound_ReturnsUnauthorized()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserState?)null);
        
        var res = await _offersService.GetUpdateQuoteAsync("auth0|abc", 3, new OfferUpdateDraftRequest());
        
        Assert.False(res.IsSuccess);
        Assert.Equal("user_not_found",res.Message);
    }
    
    [Fact]
    public async Task GetUpdateQuoteAsync_WhenUserDeleted_ReturnsUnauthorized()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, true, 1000));
        
        var res = await _offersService.GetUpdateQuoteAsync("auth0|abc", 3, new OfferUpdateDraftRequest());
        
        Assert.False(res.IsSuccess);
        Assert.Equal("user_deleted",res.Message);
    }
    
    [Fact]
    public async Task GetUpdateQuoteAsync_WhenOfferNotFound_ReturnsNotFound()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Offer?)null);
        
        var res = await _offersService.GetUpdateQuoteAsync("auth0|abc", 7, new OfferUpdateDraftRequest());
        
        Assert.False(res.IsSuccess);
        Assert.Equal("offer_not_found",res.Message);
    }
    
    [Fact]
    public async Task GetUpdateQuoteAsync_WhenOfferNotActive_ReturnsBadRequest()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer
            {
                OfferStatus_ID = (int)OfferStatuses.Expired
            });
        
        var res = await _offersService.GetUpdateQuoteAsync("auth0|abc", 7, new OfferUpdateDraftRequest());
        
        Assert.False(res.IsSuccess);
        Assert.Equal("offer_not_active",res.Message);
    }
    
    [Fact]
    public async Task GetUpdateQuoteAsync_WhenDraftInvalid_ReturnsBadRequest()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer
            {
                OfferStatus_ID = (int)OfferStatuses.Active,
                TokenCost = 20,
                ExpDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7)
            });

        var request = ValidUpdateRequest();
        request.Title = "";
        
        var res = await _offersService.GetUpdateQuoteAsync("auth0|abc", 7, request);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("title_required",res.Message);
    }
    
    [Fact]
    public async Task GetUpdateQuoteAsync_WhenValid_ReturnsQuoteWithUpdatedFee()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer
            {
                OfferStatus_ID = (int)OfferStatuses.Active,
                TokenCost = 20,
                ExpDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7)
            });
        
        SetupValidItems();

        var res = await _offersService.GetUpdateQuoteAsync("auth0|abc", 7, ValidUpdateRequest());
        
        Assert.True(res.IsSuccess);
        Assert.Equal(40, res.Data!.NewTotalCost);
        Assert.Equal(20, res.Data!.UpdateFee);
    }

    [Fact]
    public async Task GetUpdateQuoteAsync_WhenDurationChanges_AddsDurationFeeToQuote()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer
            {
                OfferStatus_ID = (int)OfferStatuses.Active,
                TokenCost = 20,
                ExpDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7)
            });
        SetupValidItems();

        var res = await _offersService.GetUpdateQuoteAsync("auth0|abc", 7, ValidUpdateRequest(durationDays: 14));
        
        Assert.True(res.IsSuccess);
        Assert.Equal(70, res.Data!.NewTotalCost);
        Assert.Equal(50, res.Data!.UpdateFee);

        
    }
    
    //CancelOfferAsync
    [Fact]
    public async Task CancelOfferAsync_WhenAuth0IdMissing_ReturnsUnauthorized()
    {
        var res = await _offersService.CancelOfferAsync(null, 3);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("missing_sub_claim",res.Message);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),Times.Never);
    }

    [Fact]
    public async Task CancelOfferAsync_TrimsAuth0Prefix_BeforeUserCheck()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserState?)null);

        var res = await _offersService.CancelOfferAsync("auth0|abc", 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("user_not_found", res.Message);
        _userRepo.Verify(x => x.GetStateByAuth0IdAsync("abc", It.IsAny<CancellationToken>()), Times.Once);
        _userRepo.Verify(x => x.GetStateByAuth0IdAsync("auth0|abc", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelOfferAsync_WhenOfferIdInvalid_ReturnsBadRequest()
    {
        var res = await _offersService.CancelOfferAsync("auth0|abc", 0);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("invalid_offer_id",res.Message);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),Times.Never);
    }
    
    [Fact]
    public async Task CancelOfferAsync_WhenUserNotFound_ReturnsUnauthorized()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserState?)null);
        
        var res = await _offersService.CancelOfferAsync("auth0|abc", 2);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("user_not_found",res.Message);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),Times.Never);
    }
    
    [Fact]
    public async Task CancelOfferAsync_WhenUserDeleted_ReturnsUnauthorized()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, true, 1000));
        
        var res = await _offersService.CancelOfferAsync("auth0|abc", 2);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("user_deleted",res.Message);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),Times.Never);
    }
    
    [Fact]
    public async Task CancelOfferAsync_WhenOfferNotFound_ReturnsNotFound()
    {
        
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Offer?)null);
        
        var res = await _offersService.CancelOfferAsync("auth0|abc", 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("offer_not_found",res.Message);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),Times.Never);
        _offersRepo.Verify(x => x.CancelOfferAsync(It.IsAny<int>(), It.IsAny<int>(),It.IsAny<CancellationToken>()),Times.Never);
    }

    [Fact]
    public async Task CancelOfferAsync_WhenOfferNotActive_ReturnsBadRequest()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer
            {
                ID = 7,
                OfferStatus_ID = (int)OfferStatuses.Expired
            });
        
        var res = await _offersService.CancelOfferAsync("auth0|abc", 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("offer_not_active",res.Message);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),Times.Never);
        _offersRepo.Verify(x => x.CancelOfferAsync(It.IsAny<int>(), It.IsAny<int>(),It.IsAny<CancellationToken>()),Times.Never);
    }

    [Fact]
    public async Task CancelOfferAsync_WhenCancelFails_RollBackAndReturnsBadRequest()
    {
        var tx = new Mock<IDbContextTransaction>();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer
            {
                ID = 7,
                OfferStatus_ID = (int)OfferStatuses.Active,
                TokensOffered = 50
            });
        _offersRepo.Setup(x => x.CancelOfferAsync(1, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var res = await _offersService.CancelOfferAsync("auth0|abc", 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("cancel_offer_failed",res.Message);
        
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),Times.Never);
    }
    
    [Fact]
    public async Task CancelOfferAsync_WhenEscrowFails_RollBackAndReturnsConflict()
    {
        var tx = new Mock<IDbContextTransaction>();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer
            {
                ID = 7,
                OfferStatus_ID = (int)OfferStatuses.Active,
                TokensOffered = 50
            });
        _offersRepo.Setup(x => x.CancelOfferAsync(1, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tokenEscrow.Setup(x => x.TryReleaseOwnEscrowAsync(1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var res = await _offersService.CancelOfferAsync("auth0|abc", 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("escrow_token_failed",res.Message);
        
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),Times.Never);
    }

    [Fact]
    public async Task CancelOfferAsync_WhenValidWithEscrowAndPendingCounterOffers_ReleasesDeniesAndCommits()
    {
        var tx = new Mock<IDbContextTransaction>();

        var pendingCounterOffer = new CounterOffer
        {
            ID = 11,
            User_ID = 99,
            TokensOffered = 30,
            CounterOfferStatus_Id = (int)CounterOfferStatuses.Pending,
            Offer_Id = 7
        };

        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer
            {
                ID = 7,
                OfferStatus_ID = (int)OfferStatuses.Active,
                TokensOffered = 50
            });
        _offersRepo.Setup(x => x.CancelOfferAsync(1, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tokenEscrow.Setup(x => x.TryReleaseOwnEscrowAsync(1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _counterOffersRepo.Setup(x => x.GetAllPendingForOfferAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CounterOffer> { pendingCounterOffer });

        var res = await _offersService.CancelOfferAsync("auth0|abc", 7);
        
        Assert.True(res.IsSuccess);
        Assert.Equal("offer_cancelled", res.Data);
        Assert.Equal((int)CounterOfferStatuses.Denied, pendingCounterOffer.CounterOfferStatus_Id);
        _tokenEscrow.Verify(x => x.TryReleaseOwnEscrowAsync(1, 50, It.IsAny<CancellationToken>()), Times.Once);
        _tokenEscrow.Verify(x => x.TryReleaseOwnEscrowAsync(99, 30, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        
    }
    
    [Fact]
    public async Task CancelOfferAsync_WhenValidWithoutEscrow_SkipOwnEscrowRelease()
    {
        var tx = new Mock<IDbContextTransaction>();

        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer
            {
                ID = 7,
                OfferStatus_ID = (int)OfferStatuses.Active,
                TokensOffered = 0
            });
        _offersRepo.Setup(x => x.CancelOfferAsync(1, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _counterOffersRepo.Setup(x => x.GetAllPendingForOfferAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CounterOffer>());

        var res = await _offersService.CancelOfferAsync("auth0|abc", 7);
        
        Assert.True(res.IsSuccess);
        Assert.Equal("offer_cancelled", res.Data);
        _tokenEscrow.Verify(x => x.TryReleaseOwnEscrowAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        
    }

    [Fact]
    public async Task CancelOfferAsync_WhenSaveThrows_RollsBackAndReturnsInternalServerError()
    {
        var tx = new Mock<IDbContextTransaction>();
        
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer
            {
                ID = 7,
                OfferStatus_ID = (int)OfferStatuses.Active,
                TokensOffered = 0
            });
        _offersRepo.Setup(x => x.CancelOfferAsync(1, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _counterOffersRepo.Setup(x => x.GetAllPendingForOfferAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CounterOffer>());
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception());

        var res = await _offersService.CancelOfferAsync("auth0|abc", 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("cancel_offer_failed", res.Message);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);

    }
    
    [Fact]
    public async Task CancelOfferAsync_WhenPendingCounterOfferHasNoTokens_DeniesWithoutReleasing()
    {
        var tx = new Mock<IDbContextTransaction>();
        
        var pendingCounterOffer = new CounterOffer
        {
            ID = 11,
            User_ID = 99,
            TokensOffered = 0,
            CounterOfferStatus_Id = (int)CounterOfferStatuses.Pending,
            Offer_Id = 7
        };
        
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer
            {
                ID = 7,
                OfferStatus_ID = (int)OfferStatuses.Active,
                TokensOffered = 0
            });
        _offersRepo.Setup(x => x.CancelOfferAsync(1, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _counterOffersRepo.Setup(x => x.GetAllPendingForOfferAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CounterOffer>{pendingCounterOffer});

        var res = await _offersService.CancelOfferAsync("auth0|abc", 7);
        
        Assert.True(res.IsSuccess);
        Assert.Equal((int)CounterOfferStatuses.Denied, pendingCounterOffer.CounterOfferStatus_Id);
        _tokenEscrow.Verify(x => x.TryReleaseOwnEscrowAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

    }
    
    //CreateOfferAsync
    [Fact]
    public async Task CreateOfferAsync_WhenAuth0IdMissing_ReturnsUnauthorized()
    {
        var res = await _offersService.CreateOfferAsync(null, new OfferDraftRequest());
        
        Assert.False(res.IsSuccess);
        Assert.Equal("missing_sub_claim", res.Message);
        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task CreateOfferAsync_WhenDraftInvalid_ReturnsBadRequest()
    {
        var req = ValidCreateRequest();
        req.Title = "";
        
        var res = await _offersService.CreateOfferAsync("auth0|abc", req);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("title_required", res.Message);
        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _userRepo.Verify(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(),It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOfferAsync_WhenUserNotFound_ReturnsUnauthorized()
    {
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserState?)null);
        
        var res = await _offersService.CreateOfferAsync("auth0|abc", ValidCreateRequest());

        Assert.False(res.IsSuccess);
        Assert.Equal("user_not_found", res.Message);
        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task CreateOfferAsync_WhenUserDeleted_ReturnsUnauthorized()
    {
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, true, 1000));
        
        var res = await _offersService.CreateOfferAsync("auth0|abc", ValidCreateRequest());

        Assert.False(res.IsSuccess);
        Assert.Equal("user_deleted", res.Message);
        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task CreateOfferAsync_WhenNotEnoughTokens_ReturnsBadRequest()
    {
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 10));
        
        var res = await _offersService.CreateOfferAsync("auth0|abc", ValidCreateRequest());

        Assert.False(res.IsSuccess);
        Assert.Equal("not_enough_tokens", res.Message);
        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOfferAsync_WhenChargeFails_RollsbackAndReturnsConflict()
    {
        var tx = new Mock<IDbContextTransaction>();
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _userRepo.Setup(x => x.TrySubtractTokenCostAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var res = await _offersService.CreateOfferAsync("auth0|abc", ValidCreateRequest());

        Assert.False(res.IsSuccess);
        Assert.Equal("concurrency_conflict", res.Message);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once());
        _offersRepo.Verify(x => x.Add(It.IsAny<Offer>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOfferAsync_WhenTokenLockFails_RollsbackAndReturnsConflict()
    {
        var tx = new Mock<IDbContextTransaction>();
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _userRepo.Setup(x => x.TrySubtractTokenCostAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tokenEscrow.Setup(x => x.TryLockOwnTokensAsync(1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        var res = await _offersService.CreateOfferAsync("auth0|abc", ValidCreateRequest(50, 50));

        Assert.False(res.IsSuccess);
        Assert.Equal("concurrency_conflict", res.Message);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once());
        _offersRepo.Verify(x => x.Add(It.IsAny<Offer>()), Times.Never);
        _userRepo.Verify(x => x.TrySubtractTokenCostAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOfferAsync_WhenValid_CreatesOfferAndReturnsCreated()
    {
        var tx = new Mock<IDbContextTransaction>();

        var offerDetails = new OfferDetailsDTO(
            new OfferCoreDTO(1, "Title", "Description", DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow, 40, 1,
                false, 0, 0),
            new OfferUserDTO(1, "Nickname", null, 0, 0f, 0f),
            new List<OfferListingItemDTO>(),
            new List<OfferListingItemDTO>());
        
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _userRepo.Setup(x => x.TrySubtractTokenCostAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _offersRepo.Setup(x => x.GetOfferWithItemsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer { ID = 1, Title = "Title", User_ID = 1 });
        _offersRepo.Setup(x => x.GetOfferWithDetailsByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offerDetails);
        
        var res = await _offersService.CreateOfferAsync("auth0|abc", ValidCreateRequest());

        Assert.True(res.IsSuccess);
        Assert.Same(offerDetails, res.Data);
        _offersRepo.Verify(x => x.Add(It.Is<Offer>(o => o.User_ID == 1 && o.OfferStatus_ID == (int)OfferStatuses.Active)), Times.Once);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _userRepo.Verify(x => x.TrySubtractTokenCostAsync(1, 40, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _tokenEscrow.Verify(x => x.TryLockOwnTokensAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _notificationSender.Verify(x => x.SendAsync(1, It.IsAny<NotificationTemplateDTO>(), It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(x => x.SendOfferCreatedAsync(1, It.IsAny<Offer>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOfferAsync_WhenDetailsFetchReturnsNull_ReturnInternalServerError()
    {
        var tx = new Mock<IDbContextTransaction>();
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _userRepo.Setup(x => x.TrySubtractTokenCostAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _offersRepo.Setup(x => x.GetOfferWithDetailsByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OfferDetailsDTO?)null);

        var res = await _offersService.CreateOfferAsync("auth0|abc", ValidCreateRequest());
        
        Assert.False(res.IsSuccess);
        Assert.Equal("create_offer_failed", res.Message);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),Times.Once);
    }

    [Fact]
    public async Task CreateOfferAsync_WhenSaveThrows_RollsbackAndReturnsInternalServerError()
    {
        var tx = new Mock<IDbContextTransaction>();
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        
        _userRepo.Setup(x => x.TrySubtractTokenCostAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("create_offer_save_throw_error"));
        var res = await _offersService.CreateOfferAsync("auth0|abc", ValidCreateRequest());

        Assert.False(res.IsSuccess);
        Assert.Equal("create_offer_save_throw_error",res.Message);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task CreateOfferAsync_WhenNotificationThrows_StillReturnsCreated()
    {
        var tx = new Mock<IDbContextTransaction>();
        var offerDetails = new OfferDetailsDTO(
            new OfferCoreDTO(1, "Title", "Description", DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow, 40, 1,
                false, 0, 0),
            new OfferUserDTO(1, "Nickname", null, 0, 0f, 0f),
            new List<OfferListingItemDTO>(),
            new List<OfferListingItemDTO>());
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        
        _userRepo.Setup(x => x.TrySubtractTokenCostAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _notificationSender.Setup(x =>
                x.SendAsync(It.IsAny<int>(), It.IsAny<NotificationTemplateDTO>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());
        _offersRepo.Setup(x => x.GetOfferWithDetailsByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offerDetails);
        var res = await _offersService.CreateOfferAsync("auth0|abc", ValidCreateRequest());

        Assert.True(res.IsSuccess);
        Assert.Equal(offerDetails,res.Data);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOfferAsync_WhenValidWithEscrowedTokens_LocksTokensAndCreates()
    {
        var tx = new Mock<IDbContextTransaction>();
        var offerDetails = new OfferDetailsDTO(
            new OfferCoreDTO(1, "Title", "Description", DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow, 40, 1,
                false, 50, 0),
            new OfferUserDTO(1, "Nickname", null, 0, 0f, 0f),
            new List<OfferListingItemDTO>(),
            new List<OfferListingItemDTO>());
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        
        _userRepo.Setup(x => x.TrySubtractTokenCostAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _tokenEscrow.Setup(x => x.TryLockOwnTokensAsync(1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _offersRepo.Setup(x => x.GetOfferWithDetailsByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offerDetails);
        
        var res = await _offersService.CreateOfferAsync("auth0|abc", ValidCreateRequest(tokensOffered:50));

        Assert.True(res.IsSuccess);
        Assert.Equal(offerDetails,res.Data);
        _tokenEscrow.Verify(x => x.TryLockOwnTokensAsync(1, 50, It.IsAny<CancellationToken>()), Times.Once);
        _offersRepo.Verify(x => x.Add(It.Is<Offer>(o => o.TokensOffered == 50)), Times.Once);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    //UpdateOfferAsync
    [Fact]
    public async Task UpdateOfferAsync_WhenAuth0IdMissing_ReturnsUnauthorized()
    {
        var res = await _offersService.UpdateOfferAsync(null, 7, new OfferUpdateDraftRequest());
        
        Assert.False(res.IsSuccess);
        Assert.Equal("missing_sub_claim", res.Message);
        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateOfferAsync_WhenOfferIdInvalid_ReturnsBadRequest()
    {
        var res = await _offersService.UpdateOfferAsync("auth0|abc", 0, new OfferUpdateDraftRequest());
        
        Assert.False(res.IsSuccess);
        Assert.Equal("invalid_offer_id", res.Message);
        _userRepo.Verify(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateOfferAsync_WhenUserNotFound_ReturnsUnauthorized()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserState?)null);
        
        var res = await _offersService.UpdateOfferAsync("auth0|abc", 1, new OfferUpdateDraftRequest());

        Assert.False(res.IsSuccess);
        Assert.Equal("user_not_found", res.Message);
        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task UpdateOfferAsync_WhenOfferNotFound_ReturnsNotFound()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Offer?)null);
        
        var res = await _offersService.UpdateOfferAsync("auth0|abc", 7, new OfferUpdateDraftRequest());

        Assert.False(res.IsSuccess);
        Assert.Equal("offer_not_found", res.Message);
        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task UpdateOfferAsync_WhenOfferNotActive_ReturnsBadRequest()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer{ID = 7, OfferStatus_ID = (int)OfferStatuses.Expired});
        
        var res = await _offersService.UpdateOfferAsync("auth0|abc", 7, new OfferUpdateDraftRequest());

        Assert.False(res.IsSuccess);
        Assert.Equal("offer_not_active", res.Message);
        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task UpdateOfferAsync_WhenDraftInvalid_ReturnsBadRequest()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveOffer());

        var req = ValidUpdateRequest();
        req.Title = "";
        
        var res = await _offersService.UpdateOfferAsync("auth0|abc", 7, req);

        Assert.False(res.IsSuccess);
        Assert.Equal("title_required", res.Message);
        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task UpdateOfferAsync_WhenNotEnoughTokens_ReturnsBadRequest()
    {
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 10));
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveOffer(tokenCost: 20));
        
        var res = await _offersService.UpdateOfferAsync("auth0|abc", 7, ValidUpdateRequest());

        Assert.False(res.IsSuccess);
        Assert.Equal("not_enough_tokens", res.Message);
        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task UpdateOfferAsync_WhenChargeFails_RollsbackReturnsConflict()
    {
        var tx = new Mock<IDbContextTransaction>();
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveOffer(tokenCost: 20));
        _userRepo.Setup(x => x.TrySubtractTokenCostAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        var res = await _offersService.UpdateOfferAsync("auth0|abc", 7, ValidUpdateRequest());

        Assert.False(res.IsSuccess);
        Assert.Equal("concurrency_conflict", res.Message);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateOfferAsync_WhenOfferedTokensIncreaseAndLockFails_RollsbackAndReturnsBadRequest()
    {
        var tx = new Mock<IDbContextTransaction>();
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveOffer(tokenCost: 20, tokensOffered: 0));
        _userRepo.Setup(x => x.TrySubtractTokenCostAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tokenEscrow.Setup(x => x.TryLockOwnTokensAsync(1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        var res = await _offersService.UpdateOfferAsync("auth0|abc", 7, ValidUpdateRequest(tokensOffered:50));

        Assert.False(res.IsSuccess);
        Assert.Equal("not_enough_tokens", res.Message);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task UpdateOfferAsync_WhenOfferedTokensDecreaseAndReleaseFails_RollsbackAndReturnsBadRequest()
    {
        var tx = new Mock<IDbContextTransaction>();
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveOffer(tokenCost: 20, tokensOffered: 50));
        _userRepo.Setup(x => x.TrySubtractTokenCostAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tokenEscrow.Setup(x => x.TryReleaseOwnEscrowAsync(1, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        var res = await _offersService.UpdateOfferAsync("auth0|abc", 7, ValidUpdateRequest(tokensOffered:20));

        Assert.False(res.IsSuccess);
        Assert.Equal("escrow_token_failed", res.Message);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task UpdateOfferAsync_WhenValid_UpdatesOfferAndReturnsSuccess()
    {
        var tx = new Mock<IDbContextTransaction>();
        var offer = ActiveOffer(tokenCost: 20, tokensOffered: 0);
        var expectedExp = offer.ExpDate;
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);
        _userRepo.Setup(x => x.TrySubtractTokenCostAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var details = UpdatedDetails();
        _offersRepo.Setup(x => x.GetOfferWithDetailsByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
        
        var res = await _offersService.UpdateOfferAsync("auth0|abc", 7, ValidUpdateRequest());

        Assert.True(res.IsSuccess);
        Assert.Equal("Title", offer.Title);
        Assert.Equal(40, offer.TokenCost);
        Assert.Equal(expectedExp, offer.ExpDate);
        Assert.Same(details, res.Data);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _userRepo.Verify(x => x.TrySubtractTokenCostAsync(1, 20, It.IsAny<CancellationToken>()), Times.Once);
        _tokenEscrow.Verify(x => x.TryLockOwnTokensAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokenEscrow.Verify(x => x.TryReleaseOwnEscrowAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    
    [Fact]
    public async Task UpdateOfferAsync_WhenListingItemsChange_AppliesAddRemoveAndQuantityUpdate()
    {
        var tx = new Mock<IDbContextTransaction>();
        var keep = new ListingItems { Item_ID = 1, IsWanted = false, Quantity = 1 };
        var wanted = new ListingItems { Item_ID = 2, IsWanted = true, Quantity = 1 };
        var toRemove = new ListingItems { Item_ID = 3, IsWanted = false, Quantity = 5 };

        var offer = new Offer
        {
            ID = 7,
            OfferStatus_ID = (int)OfferStatuses.Active,
            TokenCost = 20,
            TokensOffered = 0,
            ExpDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),
            ListingItems = new List<ListingItems>
            {
                keep, wanted, toRemove
            }
        };
        
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        _offersRepo.Setup(
                x => x.GetItemsByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new Dictionary<int, Item>
                {
                    [1] = CreateItem(1, 3, "i1", 1000),
                    [2] = CreateItem(2, 3, "i2", 1000),
                    [4] = CreateItem(4, 3, "i4", 1000),
                }
            );
        
        _userRepo.Setup(x => x.TrySubtractTokenCostAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var details = UpdatedDetails();
        _offersRepo.Setup(x => x.GetOfferWithDetailsByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var req = new OfferUpdateDraftRequest
        {
            Title = "Title",
            Description = "Description",
            DurationDays = 0,
            OfferedItems = new List<OfferItemDTO> { new(1, 2), new(4, 1) },
            WantedItems = new List<OfferItemDTO> { new(2, 1) }
        };
        
        var res = await _offersService.UpdateOfferAsync("auth0|abc", 7, req);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, keep.Quantity);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _offersRepo.Verify(x => x.RemoveListingItemsRange(It.Is<IEnumerable<ListingItems>>(items => items.Any(li => li.Item_ID == 3))), Times.Once);
        _offersRepo.Verify(x => x.AddListingItemsRange(It.Is<IEnumerable<ListingItems>>(items => items.Any(li => li.Item_ID == 4 && !li.IsWanted && li.Quantity == 1))), Times.Once);
    }
    
    [Fact]
    public async Task UpdateOfferAsync_WhenDetailsFetchNullAfterCommit_ReturnsInternalServerError()
    {
        var tx = new Mock<IDbContextTransaction>();
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveOffer(tokenCost: 20, tokensOffered:0));
        
        _userRepo.Setup(x => x.TrySubtractTokenCostAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _offersRepo.Setup(x => x.GetOfferWithDetailsByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OfferDetailsDTO?)null);
        
        
        var res = await _offersService.UpdateOfferAsync("auth0|abc", 7, ValidUpdateRequest());

        Assert.False(res.IsSuccess);
        Assert.Equal("update_offer_failed", res.Message);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task UpdateOfferAsync_WhenSaveThrows_RollsbackAndReturnsInternalServerError()
    {
        var tx = new Mock<IDbContextTransaction>();
        SetupValidItems();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetTrackedOfferAsync(7, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveOffer(tokenCost: 20, tokensOffered: 0));
        
        _userRepo.Setup(x => x.TrySubtractTokenCostAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());
        
        
        var res = await _offersService.UpdateOfferAsync("auth0|abc", 7, ValidUpdateRequest());

        Assert.False(res.IsSuccess);
        Assert.Equal("update_offer_failed", res.Message);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        
    }
    
    //AcceptOfferAsync
    [Fact]
    public async Task AcceptOfferAsync_WhenAuth0IdMissing_ReturnsUnauthorized()
    {
        var res = await _offersService.AcceptOfferAsync(null, 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("missing_sub_claim", res.Message);
        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task AcceptOfferAsync_WhenOfferIdInvalid_ReturnsBadRequest()
    {
        var res = await _offersService.AcceptOfferAsync("auth0|abc", 0);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("invalid_offer_id", res.Message);
        _userRepo.Verify(x => x.GetStateByAuth0IdAsync(It.IsAny<string>() ,It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task AcceptOfferAsync_WhenUserNotFound_ReturnsUnauthorized()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserState?)null);
        
        var res = await _offersService.AcceptOfferAsync("auth0|abc", 1);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("user_not_found", res.Message);
        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task AcceptOfferAsync_WhenBuyerNotFound_ReturnsUnauthorized()
    {
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _userRepo.Setup(x => x.GetNotificationDataByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserNotificationData?)null);
        
        var res = await _offersService.AcceptOfferAsync("auth0|abc", 1);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("buyer_not_found", res.Message);
        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task AcceptOfferAsync_WhenOfferNotFound_ReturnsNotFound()
    {
        var tx = new Mock<IDbContextTransaction>();
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _userRepo.Setup(x => x.GetNotificationDataByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Buyer());
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetOfferByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Offer?)null);
        
        var res = await _offersService.AcceptOfferAsync("auth0|abc", 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("offer_not_found", res.Message);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task AcceptOfferAsync_WhenAcceptingOwnOffer_ReturnsBadRequest()
    {
        var tx = new Mock<IDbContextTransaction>();
        var offer = ActiveOffer(tokensOffered: 0);
        offer.User_ID = 1;
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _userRepo.Setup(x => x.GetNotificationDataByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Buyer());
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetOfferByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);
        
        var res = await _offersService.AcceptOfferAsync("auth0|abc", 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("cannot_accept_own_offer", res.Message);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _offersRepo.Verify(x => x.SetOfferInRealizationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task AcceptOfferAsync_WhenOfferNotActive_ReturnsBadRequest()
    {
        var tx = new Mock<IDbContextTransaction>();
        var offer = ActiveOffer(tokensOffered: 0);
        offer.User_ID = 2;
        offer.OfferStatus_ID = (int)OfferStatuses.Expired;
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _userRepo.Setup(x => x.GetNotificationDataByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Buyer());
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetOfferByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);
        
        var res = await _offersService.AcceptOfferAsync("auth0|abc", 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("offer_not_active", res.Message);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _offersRepo.Verify(x => x.SetOfferInRealizationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task AcceptOfferAsync_WhenOfferExpired_ReturnsBadRequest()
    {
        var tx = new Mock<IDbContextTransaction>();
        var offer = ActiveOffer(tokensOffered: 0);
        offer.User_ID = 2;
        offer.ExpDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _userRepo.Setup(x => x.GetNotificationDataByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Buyer());
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetOfferByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);
        
        var res = await _offersService.AcceptOfferAsync("auth0|abc", 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("offer_expired", res.Message);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _offersRepo.Verify(x => x.SetOfferInRealizationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task AcceptOfferAsync_WhenTradeExists_ReturnsConflict()
    {
        var tx = new Mock<IDbContextTransaction>();
        var offer = ActiveOffer(tokensOffered: 0);
        offer.User_ID = 2;
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _userRepo.Setup(x => x.GetNotificationDataByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Buyer());
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetOfferByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);
        _tradesRepo.Setup(x => x.TradeExistsForOfferAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        
        var res = await _offersService.AcceptOfferAsync("auth0|abc", 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("trade_already_exists", res.Message);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _offersRepo.Verify(x => x.SetOfferInRealizationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task AcceptOfferAsync_WhenSetInRealizationFails_ReturnsConflict()
    {
        var tx = new Mock<IDbContextTransaction>();
        var offer = ActiveOffer(tokensOffered: 0);
        offer.User_ID = 2;
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _userRepo.Setup(x => x.GetNotificationDataByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Buyer());
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetOfferByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);
        _tradesRepo.Setup(x => x.TradeExistsForOfferAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _offersRepo.Setup(x => x.SetOfferInRealizationAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        
        var res = await _offersService.AcceptOfferAsync("auth0|abc", 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("update_offer_status_failed", res.Message);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task AcceptOfferAsync_WhenOfferedTokensTransferFails_RollsbackAndReturnsConflict()
    {
        var tx = new Mock<IDbContextTransaction>();
        var offer = ActiveOffer(tokensOffered: 50);
        offer.User_ID = 2;
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _userRepo.Setup(x => x.GetNotificationDataByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Buyer());
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetOfferByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);
        _tradesRepo.Setup(x => x.TradeExistsForOfferAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _offersRepo.Setup(x => x.SetOfferInRealizationAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _counterOffersRepo.Setup(x => x.GetAllPendingForOfferAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CounterOffer>());
        _tokenEscrow.Setup(x => x.TryTransferEscrowAsync(2, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        var res = await _offersService.AcceptOfferAsync("auth0|abc", 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("escrow_failed", res.Message);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    
    [Fact]
    public async Task AcceptOfferAsync_WhenWantedTokensEscrowFails_RollsbackAndReturnsConflict()
    {
        var tx = new Mock<IDbContextTransaction>();
        var offer = ActiveOffer(tokensOffered: 0);
        offer.User_ID = 2;
        offer.TokensWanted = 50;
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _userRepo.Setup(x => x.GetNotificationDataByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Buyer());
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetOfferByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);
        _tradesRepo.Setup(x => x.TradeExistsForOfferAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _offersRepo.Setup(x => x.SetOfferInRealizationAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _counterOffersRepo.Setup(x => x.GetAllPendingForOfferAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CounterOffer>());
        _tokenEscrow.Setup(x => x.TryEscrowToOtherAsync(1, 2, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        var res = await _offersService.AcceptOfferAsync("auth0|abc", 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("escrow_failed", res.Message);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task AcceptOfferAsync_WhenSellerNotFound_RollsbackAndReturnsNotFound()
    {
        var tx = new Mock<IDbContextTransaction>();
        var offer = ActiveOffer(tokensOffered: 0);
        offer.User_ID = 2;
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _userRepo.Setup(x => x.GetNotificationDataByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Buyer());
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetOfferByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);
        _tradesRepo.Setup(x => x.TradeExistsForOfferAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _offersRepo.Setup(x => x.SetOfferInRealizationAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _counterOffersRepo.Setup(x => x.GetAllPendingForOfferAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CounterOffer>());
        _tradeCreation.Setup(x => x.ExecuteAsync(It.IsAny<CreateTradeContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade { ID = 99 });
        _userRepo.Setup(x => x.GetNotificationDataByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserNotificationData?)null);
        
        var res = await _offersService.AcceptOfferAsync("auth0|abc", 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("seller_not_found", res.Message);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task AcceptOfferAsync_WhenValid_CreatesTradeAndReturnsSuccess()
    {
        var tx = new Mock<IDbContextTransaction>();
        var offer = ActiveOffer(tokensOffered: 50);
        offer.User_ID = 2;
        offer.TokensWanted = 30;
        offer.Title = "SwordOffer";
        var pendingCo = new CounterOffer()
        {
            ID = 11,
            User_ID = 97,
            TokensOffered = 15,
            CounterOfferStatus_Id = (int)CounterOfferStatuses.Pending,
            Offer_Id = 7
        };
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _userRepo.Setup(x => x.GetNotificationDataByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Buyer());
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetOfferByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);
        _tradesRepo.Setup(x => x.TradeExistsForOfferAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _offersRepo.Setup(x => x.SetOfferInRealizationAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _counterOffersRepo.Setup(x => x.GetAllPendingForOfferAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CounterOffer>{pendingCo});
        _tokenEscrow.Setup(x => x.TryTransferEscrowAsync(2, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tokenEscrow.Setup(x => x.TryEscrowToOtherAsync(1, 2, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _tradeCreation.Setup(x => x.ExecuteAsync(It.IsAny<CreateTradeContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade { ID = 99 });
        _userRepo.Setup(x => x.GetNotificationDataByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Seller());
        
        var res = await _offersService.AcceptOfferAsync("auth0|abc", 7);
        
        Assert.True(res.IsSuccess);
        Assert.Equal(99, res.Data!.TradeId);
        Assert.Equal(7, res.Data!.OfferId);
        Assert.Equal((int)CounterOfferStatuses.Denied, pendingCo.CounterOfferStatus_Id);
        _tradeCreation.Verify(x => x.ExecuteAsync(It.Is<CreateTradeContext>(c => c.OfferId ==7 && c.BuyerId == 1 && c.SellerId == 2), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notificationSender.Verify(x => x.SendAsync(2, It.IsAny<NotificationTemplateDTO>(), It.IsAny<CancellationToken>()), Times.Once);
        _notificationSender.Verify(x => x.SendAsync(1, It.IsAny<NotificationTemplateDTO>(), It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(x => x.SendTradeCreatedAsync(2, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Trade>(), It.IsAny<Offer>(), It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(x => x.SendTradeCreatedAsync(1, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Trade>(), It.IsAny<Offer>(), It.IsAny<CancellationToken>()), Times.Once);
        _tokenEscrow.Verify(x => x.TryReleaseOwnEscrowAsync(97, 15, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task AcceptOfferAsync_WhenNotificationThrows_StillReturnsSuccess()
    {
        var tx = new Mock<IDbContextTransaction>();
        var offer = ActiveOffer(tokensOffered: 0);
        offer.User_ID = 2;
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _userRepo.Setup(x => x.GetNotificationDataByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Buyer());
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetOfferByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);
        _tradesRepo.Setup(x => x.TradeExistsForOfferAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _offersRepo.Setup(x => x.SetOfferInRealizationAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _counterOffersRepo.Setup(x => x.GetAllPendingForOfferAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CounterOffer>());
        _tradeCreation.Setup(x => x.ExecuteAsync(It.IsAny<CreateTradeContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade { ID = 99 });
        _userRepo.Setup(x => x.GetNotificationDataByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Seller());
        _notificationSender.Setup(x =>
                x.SendAsync(It.IsAny<int>(), It.IsAny<NotificationTemplateDTO>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());
        
        var res = await _offersService.AcceptOfferAsync("auth0|abc", 7);
        
        Assert.True(res.IsSuccess);
        Assert.Equal(99, res.Data!.TradeId);
        Assert.Equal(7, res.Data!.OfferId);
        _tradeCreation.Verify(x => x.ExecuteAsync(It.Is<CreateTradeContext>(c => c.OfferId ==7 && c.BuyerId == 1 && c.SellerId == 2), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    
    
    [Fact]
    public async Task AcceptOfferAsync_WhenSaveThrows_RollsbackAndReturnsInternalServerError()
    {
        var tx = new Mock<IDbContextTransaction>();
        var offer = ActiveOffer(tokensOffered: 0);
        offer.User_ID = 2;
        _userRepo.Setup(x => x.GetStateByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserState(1, false, 1000));
        _userRepo.Setup(x => x.GetNotificationDataByAuth0IdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Buyer());
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tx.Object);
        _offersRepo.Setup(x => x.GetOfferByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);
        _tradesRepo.Setup(x => x.TradeExistsForOfferAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _offersRepo.Setup(x => x.SetOfferInRealizationAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _counterOffersRepo.Setup(x => x.GetAllPendingForOfferAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CounterOffer>());
        _tradeCreation.Setup(x => x.ExecuteAsync(It.IsAny<CreateTradeContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade { ID = 99 });
        _userRepo.Setup(x => x.GetNotificationDataByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Seller());
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());
        
        var res = await _offersService.AcceptOfferAsync("auth0|abc", 7);
        
        Assert.False(res.IsSuccess);
        Assert.Equal("accept_offer_failed", res.Message);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}