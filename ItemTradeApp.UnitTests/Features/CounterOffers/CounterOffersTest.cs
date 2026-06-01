using ItemTradeApp.Features.CounterOffers;
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
        // Zabezpieczenie przed NullReferenceException przy await na mockowanych metodach async.
        _unitOfWork.SetReturnsDefault(Task.CompletedTask);
        _unitOfWork.SetReturnsDefault(Task.FromResult(1));
        _notificationSender.SetReturnsDefault(Task.CompletedTask);
        _tradeCreation.SetReturnsDefault(Task.CompletedTask);
        _tokenEscrow.SetReturnsDefault(Task.CompletedTask);
        _emailService.SetReturnsDefault(Task.CompletedTask);
        _emailService.SetReturnsDefault("test email body");

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
    public async Task CreateCounterOfferAsync_WhenAuth0UserIdIsMissing_ReturnsUnauthorized()
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
    public async Task CreateCounterOfferAsync_WhenOfferIdIsInvalid_ReturnsBadRequest()
    {
        var req = ValidRequest();

        var result = await _service.CreateCounterOfferAsync(
            "auth0|user",
            0,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Niepoprawne ID oferty", result.Message);
    }

    [Fact]
    public async Task CreateCounterOfferAsync_WhenOfferDoesNotExist_ReturnsNotFound()
    {
        var req = ValidRequest();

        _userRepo
            .Setup(x => x.GetUserInfo("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 1000,
                Email = "test@test.com"
            });

        _offerRepo
            .Setup(x => x.GetOfferAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Offer?)null);

        var result = await _service.CreateCounterOfferAsync(
            "auth0|user",
            1,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Nie znaleziono oferty", result.Message);
    }

    [Fact]
    public async Task CreateCounterOfferAsync_WhenUserCountersOwnOffer_ReturnsBadRequest()
    {
        var req = ValidRequest();

        _userRepo
            .Setup(x => x.GetUserInfo("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 1000,
                Email = "test@test.com"
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
            "auth0|user",
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
    public async Task CreateCounterOfferAsync_WhenTokensOfferedAreNegative_ReturnsBadRequest()
    {
        var req = ValidRequest(tokensOffered: -100);

        var result = await _service.CreateCounterOfferAsync(
            "auth0|user",
            1,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Niepoprawna ilość tokenów", result.Message);

        _counterOfferRepo.Verify(
            x => x.AddCounterOffer(It.IsAny<CounterOffer>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateCounterOfferAsync_WhenUserDoesNotExist_ReturnsUnauthorized()
    {
        var req = ValidRequest();

        _userRepo
            .Setup(x => x.GetUserInfo("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _service.CreateCounterOfferAsync(
            "auth0|user",
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
    public async Task CreateCounterOfferAsync_WhenOfferIsNotActive_ReturnsBadRequest()
    {
        var req = ValidRequest();

        _userRepo
            .Setup(x => x.GetUserInfo("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 1000,
                Email = "test@test.com"
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
            "auth0|user",
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
    public async Task CreateCounterOfferAsync_WhenOfferIsExpired_ReturnsBadRequest()
    {
        var req = ValidRequest();

        _userRepo
            .Setup(x => x.GetUserInfo("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 1000,
                Email = "test@test.com"
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
            "auth0|user",
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
    public async Task CreateCounterOfferAsync_WhenItemDoesNotExist_ReturnsBadRequest()
    {
        var req = ValidRequest();

        _userRepo
            .Setup(x => x.GetUserInfo("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 1000,
                Email = "test@test.com"
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
                    Tokens = 1000,
                    Email = "owner@test.com"
                },
                OfferStatus_ID = (int)OfferStatuses.Active,
                ExpDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
            });

        _itemsRepo
            .Setup(x => x.AllItemsExistAsync(
                It.IsAny<int[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(false));

        var result = await _service.CreateCounterOfferAsync(
            "auth0|user",
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
    public async Task QuoteCounterOfferAsync_WhenAuth0UserIdIsMissing_ReturnsUnauthorized()
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
    public async Task QuoteCounterOfferAsync_WhenOfferIdIsInvalid_ReturnsBadRequest()
    {
        var req = ValidRequest();

        var result = await _service.QuoteCounterOfferAsync(
            "auth0|user",
            0,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Niepoprawne ID oferty", result.Message);
    }

    [Fact]
    public async Task QuoteCounterOfferAsync_WhenTokensOfferedAreNegative_ReturnsBadRequest()
    {
        var req = ValidRequest(tokensOffered: -100);

        var result = await _service.QuoteCounterOfferAsync(
            "auth0|user",
            1,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Niepoprawna ilość tokenów", result.Message);
    }

    [Fact]
    public async Task QuoteCounterOfferAsync_WhenOfferDoesNotExist_ReturnsNotFound()
    {
        var req = ValidRequest();

        _userRepo
            .Setup(x => x.GetUserInfo("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 1000,
                Email = "test@test.com"
            });

        _offerRepo
            .Setup(x => x.GetOfferAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Offer?)null);

        var result = await _service.QuoteCounterOfferAsync(
            "auth0|user",
            1,
            req,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Nie znaleziono oferty", result.Message);
    }

    [Fact]
    public async Task QuoteCounterOfferAsync_WhenRequestIsValid_ReturnsSuccess()
    {
        var req = ValidRequest();

        _userRepo
            .Setup(x => x.GetUserInfo("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 1000,
                Email = "test@test.com"
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
            .Returns(Task.FromResult(true));

        var result = await _service.QuoteCounterOfferAsync(
            "auth0|user",
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
    public async Task CancelCounterOfferAsync_WhenCounterOfferDoesNotExist_ReturnsNotFound()
    {
        _userRepo
            .Setup(x => x.GetUserInfo("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 1000,
                Email = "test@test.com"
            });

        _counterOfferRepo
            .Setup(x => x.GetCounterOfferWithOfferAndItemsAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CounterOffer?)null);

        var result = await _service.CancelCounterOfferAsync(
            "auth0|user",
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Nie znaleziono kontroferty", result.Message);
    }

    [Fact]
    public async Task CancelCounterOfferAsync_WhenUserIsNotOwner_ReturnsForbidden()
    {
        _userRepo
            .Setup(x => x.GetUserInfo("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 1000,
                Email = "test@test.com"
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
            "auth0|user",
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess, result.Message);
    }

    [Fact]
    public async Task AcceptCounterOfferAsync_WhenAuth0UserIdIsMissing_ReturnsUnauthorized()
    {
        var result = await _service.AcceptCounterOfferAsync(
            null,
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("missing_sub_claim", result.Message);
    }

    [Fact]
    public async Task AcceptCounterOfferAsync_WhenCounterOfferDoesNotExist_ReturnsNotFound()
    {
        _userRepo
            .Setup(x => x.GetUserInfo("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 1000,
                Email = "test@test.com"
            });

        _counterOfferRepo
            .Setup(x => x.GetCounterOfferWithOfferAndItemsAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CounterOffer?)null);

        var result = await _service.AcceptCounterOfferAsync(
            "auth0|user",
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("KO nie znalezione", result.Message);
    }

    [Fact]
    public async Task AcceptCounterOfferAsync_WhenUserIsNotOfferOwner_ReturnsForbidden()
    {
        _userRepo
            .Setup(x => x.GetUserInfo("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = 1,
                Tokens = 1000,
                Email = "test@test.com"
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
            "auth0|user",
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess, result.Message);
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