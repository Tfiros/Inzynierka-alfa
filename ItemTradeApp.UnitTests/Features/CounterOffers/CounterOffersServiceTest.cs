using ItemTradeApp.Features.CounterOffers;
using ItemTradeApp.Features.CounterOffers.DTOs;
using ItemTradeApp.Features.CounterOffers.DTOs.RequestDTOs;
using ItemTradeApp.Features.CounterOffers.Repositories;
using ItemTradeApp.Features.Shared.DTOs;
using ItemTradeApp.Features.Shared.Emails.Services;
using ItemTradeApp.Features.Shared.Notifications;
using ItemTradeApp.Features.Shared.TokenEscrow;
using ItemTradeApp.Features.Shared.TradeCreation;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace ItemTradeApp.UnitTests.Features.CounterOffers;

[TestSubject(typeof(CounterOffersService))]
public class CounterOffersServiceTest
{
    private readonly Mock<ICounterOffersRepository> _counterOfferRepo = new();
    private readonly Mock<IOfferRepository> _offerRepo = new();
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<IItemsRepository> _itemsRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ITokenEscrow> _tokenEscrow = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITradeCreation> _tradeCreation = new();
    private readonly Mock<INotificationSender> _notificationSender = new();
    private readonly Mock<IEmailGenerationService> _emailService = new();

    private readonly CounterOffersService _service;

    public CounterOffersServiceTest()
    {
        _service = new CounterOffersService(
            _counterOfferRepo.Object,
            _offerRepo.Object,
            _tradeRepo.Object,
            _itemsRepo.Object,
            _userRepo.Object,
            _tokenEscrow.Object,
            _unitOfWork.Object,
            _tradeCreation.Object,
            _notificationSender.Object,
            _emailService.Object
        );
    }

    [Fact]
    public async Task CreateCounterOfferAsync_MissingToken()
    {
        var req = ValidRequest();

        var result = await _service.CreateCounterOfferAsync(
            null,
            1,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("missing_sub_claim", result.Message);

        _counterOfferRepo.Verify(
            x => x.AddCounterOffer(It.IsAny<CounterOffer>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateCounterOfferAsync_InvalidOffer()
    {
        var req = ValidRequest();

        var result = await _service.CreateCounterOfferAsync(
            "192038210",
            0,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Niepoprawne ID oferty", result.Message);
    }

    [Fact]
    public async Task CreateCounterOfferAsync_OfferDoesntExist()
    {
        var req = ValidRequest();

        _userRepo
            .Setup(x => x.GetUserInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "testowy@test.com"
            });

        _offerRepo
            .Setup(x => x.GetOfferAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Offer?)null);

        var result = await _service.CreateCounterOfferAsync(
            "12903123",
            1,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Nie znaleziono oferty", result.Message);
    }

    [Fact]
    public async Task CreateCounterOfferAsync_CounterOwnOffer()
    {
        var req = ValidRequest();

        _userRepo
            .Setup(x => x.GetUserInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "testowy@test.com"
            });

        _offerRepo
            .Setup(x => x.GetOfferAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer
            {
                ID = 1,
                User_ID = 1,
                OfferStatus_ID = (int)OfferStatuses.Active,
                ExpDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
            });

        var result = await _service.CreateCounterOfferAsync(
            "129038120938",
            1,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Nie można złożyć kontroferty do swojej oferty", result.Message);

        _counterOfferRepo.Verify(
            x => x.AddCounterOffer(It.IsAny<CounterOffer>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateCounterOfferAsync_UserDoesntExist()
    {
        var req = ValidRequest();

        _userRepo
            .Setup(x => x.GetUserInfo("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _service.CreateCounterOfferAsync(
            "19083213",
            1,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("użytkownik", result.Message, StringComparison.OrdinalIgnoreCase);

        _counterOfferRepo.Verify(
            x => x.AddCounterOffer(It.IsAny<CounterOffer>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateCounterOfferAsync_OfferIsntActive()
    {
        var req = ValidRequest();

        _userRepo
            .Setup(x => x.GetUserInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "testowy@test.com"
            });

        _offerRepo
            .Setup(x => x.GetOfferAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer
            {
                ID = 1,
                User_ID = 2,
                OfferStatus_ID = (int)OfferStatuses.Canceled,
                ExpDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
            });

        var result = await _service.CreateCounterOfferAsync(
            "09123712",
            1,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("aktyw", result.Message, StringComparison.OrdinalIgnoreCase);

        _counterOfferRepo.Verify(
            x => x.AddCounterOffer(It.IsAny<CounterOffer>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateCounterOfferAsync_ExpiredOffer()
    {
        var req = ValidRequest();

        _userRepo
            .Setup(x => x.GetUserInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "testowy@test.com"
            });

        _offerRepo
            .Setup(x => x.GetOfferAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer
            {
                ID = 1,
                User_ID = 2,
                OfferStatus_ID = (int)OfferStatuses.Active,
                ExpDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
            });

        var result = await _service.CreateCounterOfferAsync(
            "1298123",
            1,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Oferta wygasła", result.Message);

        _counterOfferRepo.Verify(
            x => x.AddCounterOffer(It.IsAny<CounterOffer>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateCounterOfferAsync_ItemDoesntExist()
    {
        var req = ValidRequest();

        _userRepo
            .Setup(x => x.GetUserInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "testowy@test.com"
            });

        _offerRepo
            .Setup(x => x.GetOfferAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer
            {
                ID = 1,
                User_ID = 2,
                User = new User
                {
                    ID = 2,
                    Tokens = 10000,
                    Email = "owner@test.com"
                },
                OfferStatus_ID = (int)OfferStatuses.Active,
                ExpDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
            });

        _itemsRepo
            .Setup(x => x.AllItemsExistAsync(
                It.IsAny<int[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CreateCounterOfferAsync(
            "1298313",
            1,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Jeden z przedmiotów nie istnieje.", result.Message);

        _counterOfferRepo.Verify(
            x => x.AddCounterOffer(It.IsAny<CounterOffer>()),
            Times.Never);
    }

    [Fact]
    public async Task QuoteCounterOfferAsync_Auth0Missing()
    {
        var req = ValidRequest();

        var result = await _service.QuoteCounterOfferAsync(
            null,
            1,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("missing_sub_claim", result.Message);
    }

    [Fact]
    public async Task QuoteCounterOfferAsync_InvalidOffer()
    {
        var req = ValidRequest();

        var result = await _service.QuoteCounterOfferAsync(
            "0912873",
            0,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Niepoprawne ID oferty", result.Message);
    }

    [Fact]
    public async Task QuoteCounterOfferAsync_NegativeTokens()
    {
        var req = ValidRequest(tokensOffered: -100);

        var result = await _service.QuoteCounterOfferAsync(
            "8912368912",
            1,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Niepoprawna ilość tokenów", result.Message);
    }

    [Fact]
    public async Task QuoteCounterOfferAsync_OfferDoesntExist()
    {
        var req = ValidRequest();

        _userRepo
            .Setup(x => x.GetUserInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "testowy@test.com"
            });

        _offerRepo
            .Setup(x => x.GetOfferAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Offer?)null);

        var result = await _service.QuoteCounterOfferAsync(
            "12903813",
            1,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Nie znaleziono oferty", result.Message);
    }

    [Fact]
    public async Task QuoteCounterOfferAsync_Valid()
    {
        var req = ValidRequest();

        _userRepo
            .Setup(x => x.GetUserInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "testowy@test.com"
            });

        _offerRepo
            .Setup(x => x.GetOfferAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Offer
            {
                ID = 1,
                User_ID = 2,
                OfferStatus_ID = (int)OfferStatuses.Active,
                ExpDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
            });

        _itemsRepo
            .Setup(x => x.AllItemsExistAsync(
                It.IsAny<int[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.QuoteCounterOfferAsync(
            "120831",
            1,
            req,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task CancelCounterOfferAsync_WhenAuth0UserIdIsMissing_ReturnsUnauthorized()
    {
        var result = await _service.CancelCounterOfferAsync(
            null,
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("missing_sub_claim", result.Message);
    }

    [Fact]
    public async Task CancelCounterOfferAsync_CODoesntExist()
    {
        _userRepo
            .Setup(x => x.GetUserInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "testowy@test.com"
            });

        _counterOfferRepo
            .Setup(x => x.GetCounterOfferWithOfferAndItemsAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CounterOffer?)null);

        var result = await _service.CancelCounterOfferAsync(
            "1923213",
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Nie znaleziono kontroferty", result.Message);
    }

    [Fact]
    public async Task CancelCounterOfferAsync_UserNotOwner()
    {
        _userRepo
            .Setup(x => x.GetUserInfo("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "testowy@test.com"
            });

        _counterOfferRepo
            .Setup(x => x.GetCounterOfferWithOfferAndItemsAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CounterOffer
            {
                ID = 1,
                User_ID = 2
            });

        var result = await _service.CancelCounterOfferAsync(
            "9871263",
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess, result.Message);
    }

    [Fact]
    public async Task AcceptCounterOfferAsync_Auth0Missing()
    {
        var result = await _service.AcceptCounterOfferAsync(
            null,
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("missing_sub_claim", result.Message);
    }

    [Fact]
    public async Task AcceptCounterOfferAsync_CODoesntExist()
    {
        _userRepo
            .Setup(x => x.GetUserInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "testowy@test.com"
            });

        _counterOfferRepo
            .Setup(x => x.GetCounterOfferWithOfferAndItemsAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CounterOffer?)null);

        var result = await _service.AcceptCounterOfferAsync(
            "0987123",
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("KO nie znalezione", result.Message);
    }

    [Fact]
    public async Task AcceptCounterOfferAsync_UserIsNotOwner()
    {
        _userRepo
            .Setup(x => x.GetUserInfo("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "testowy@test.com"
            });

        _counterOfferRepo
            .Setup(x => x.GetCounterOfferWithOfferAndItemsAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CounterOffer
            {
                ID = 1,
                User_ID = 2,
                Offer = new Offer
                {
                    ID = 1,
                    User_ID = 3,
                    User = new User
                    {
                        ID = 3,
                        Tokens = 1000,
                        Email = "owner@test.com"
                    },
                    OfferStatus_ID = (int)OfferStatuses.Active,
                    ExpDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
                }
            });

        var result = await _service.AcceptCounterOfferAsync(
            "0897213",
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess, result.Message);
    }

        [Fact]
    public async Task CancelCounterOfferAsync_PendingCounterOfferWithoutTokens()
    {
        var transaction = new Mock<IDbContextTransaction>();

        _unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);

        _userRepo
            .Setup(x => x.GetUserInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "testowy@test.com"
            });

        _counterOfferRepo
            .Setup(x => x.GetCounterOfferWithOfferAndItemsAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CounterOffer
            {
                ID = 1,
                User_ID = 1,
                Offer_Id = 10,
                TokensOffered = 0,
                CreationDate = DateTime.UtcNow,
                CounterOfferStatus_Id = (int)CounterOfferStatuses.Pending,
                ListingCounterOfferItems = new List<ListingCounterOfferItem>
                {
                    new()
                    {
                        Item_ID = 1,
                        Quantity = 1
                    }
                }
            });

        var result = await _service.CancelCounterOfferAsync(
            "user",
            1,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal((int)CounterOfferStatuses.Denied, result.Data.CounterOfferStatusId);

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelCounterOfferAsync_NotPending()
    {
        var transaction = new Mock<IDbContextTransaction>();

        _unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);

        _userRepo
            .Setup(x => x.GetUserInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "testowy@test.com"
            });

        _counterOfferRepo
            .Setup(x => x.GetCounterOfferWithOfferAndItemsAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CounterOffer
            {
                ID = 1,
                User_ID = 1,
                CounterOfferStatus_Id = (int)CounterOfferStatuses.Accepted
            });

        var result = await _service.CancelCounterOfferAsync(
            "user",
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Kontroferta nie jest oczekująca", result.Message);

        _unitOfWork.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AcceptCounterOfferAsync_InvalidCounterOfferId()
    {
        var result = await _service.AcceptCounterOfferAsync(
            "user",
            0,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Niepoprawne ID KO", result.Message);
    }

    [Fact]
    public async Task AcceptCounterOfferAsync_OfferMissing()
    {
        var transaction = new Mock<IDbContextTransaction>();

        _unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);

        _userRepo
            .Setup(x => x.GetUserInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "owner@test.com"
            });

        _counterOfferRepo
            .Setup(x => x.GetCounterOfferWithOfferAndItemsAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CounterOffer
            {
                ID = 1,
                User_ID = 2,
                Offer = null
            });

        var result = await _service.AcceptCounterOfferAsync(
            "user",
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Oferta nie znaleziona", result.Message);
    }

    [Fact]
    public async Task AcceptCounterOfferAsync_CounterOffer_NotPending()
    {
        var transaction = new Mock<IDbContextTransaction>();

        _unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);

        _userRepo
            .Setup(x => x.GetUserInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "owner@test.com"
            });

        _counterOfferRepo
            .Setup(x => x.GetCounterOfferWithOfferAndItemsAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CounterOffer
            {
                ID = 1,
                User_ID = 2,
                CounterOfferStatus_Id = (int)CounterOfferStatuses.Denied,
                Offer = new Offer
                {
                    ID = 10,
                    User_ID = 1,
                    OfferStatus_ID = (int)OfferStatuses.Active,
                    ExpDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
                }
            });

        var result = await _service.AcceptCounterOfferAsync(
            "user",
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Niepoprawny status kontroferty", result.Message);
    }

    [Fact]
    public async Task AcceptCounterOfferAsync_Offer_NotActive()
    {
        var transaction = new Mock<IDbContextTransaction>();

        _unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);

        _userRepo
            .Setup(x => x.GetUserInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "owner@test.com"
            });

        _counterOfferRepo
            .Setup(x => x.GetCounterOfferWithOfferAndItemsAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CounterOffer
            {
                ID = 1,
                User_ID = 2,
                CounterOfferStatus_Id = (int)CounterOfferStatuses.Pending,
                Offer = new Offer
                {
                    ID = 10,
                    User_ID = 1,
                    OfferStatus_ID = (int)OfferStatuses.Canceled,
                    ExpDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
                }
            });

        var result = await _service.AcceptCounterOfferAsync(
            "user",
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Oferta nie jest aktywna", result.Message);
    }

    [Fact]
    public async Task AcceptCounterOfferAsync_Offer_Expired()
    {
        var transaction = new Mock<IDbContextTransaction>();

        _unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);

        _userRepo
            .Setup(x => x.GetUserInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "owner@test.com"
            });

        _counterOfferRepo
            .Setup(x => x.GetCounterOfferWithOfferAndItemsAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CounterOffer
            {
                ID = 1,
                User_ID = 2,
                CounterOfferStatus_Id = (int)CounterOfferStatuses.Pending,
                Offer = new Offer
                {
                    ID = 10,
                    User_ID = 1,
                    OfferStatus_ID = (int)OfferStatuses.Active,
                    ExpDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
                }
            });

        var result = await _service.AcceptCounterOfferAsync(
            "user",
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Oferta jest przeterminowana", result.Message);
    }

    [Fact]
    public async Task AcceptCounterOfferAsync_TradeExists()
    {
        var transaction = new Mock<IDbContextTransaction>();

        _unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);

        _userRepo
            .Setup(x => x.GetUserInfo(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 10000,
                Email = "owner@test.com"
            });

        _counterOfferRepo
            .Setup(x => x.GetCounterOfferWithOfferAndItemsAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CounterOffer
            {
                ID = 1,
                User_ID = 2,
                CounterOfferStatus_Id = (int)CounterOfferStatuses.Pending,
                Offer = new Offer
                {
                    ID = 10,
                    User_ID = 1,
                    OfferStatus_ID = (int)OfferStatuses.Active,
                    ExpDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
                }
            });

        _tradeRepo
            .Setup(x => x.TradeExistsForOfferAsync(
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.AcceptCounterOfferAsync(
            "user",
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade już istnieje", result.Message);
    }
    
    private static CounterOfferDraftRequest ValidRequest(int tokensOffered = 100)
    {
        return new CounterOfferDraftRequest(
            new List<OfferItemDTO>
            {
                new(1, Quantity: 1)
            },
            tokensOffered
        );
    }
}