using ItemTradeApp.Features.Shared.Chat;
using ItemTradeApp.Features.Shared.Emails.Services;
using ItemTradeApp.Features.Shared.Images;
using ItemTradeApp.Features.Shared.Notifications;
using ItemTradeApp.Features.Shared.TokenEscrow;
using ItemTradeApp.Features.Trades;
using ItemTradeApp.Features.Trades.DTOs;
using ItemTradeApp.Features.Trades.DTOs.Request;
using ItemTradeApp.Features.Trades.Repositories;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace ItemTradeApp.UnitTests.Features.Trades;

public class TradesServiceTest
{
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ITradesRequestValidator> _validator = new();
    private readonly Mock<ITradeListQueryService> _listQuery = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITokenEscrow> _tokenEscrow = new();
    private readonly Mock<IChatOperations> _chatOperations = new();
    private readonly Mock<INotificationSender> _notificationsSender = new();
    private readonly Mock<IEmailGenerationService> _emailGenerationService = new();
    private readonly Mock<IImageService> _imageService = new();

    private readonly TradesService _service;

    public TradesServiceTest()
    {
        var folders = Options.Create(new S3Folders
        {
            TradeConfirmations = "trade-confirmations"
        });

        _service = new TradesService(
            _tradeRepo.Object,
            _userContext.Object,
            _validator.Object,
            _listQuery.Object,
            _unitOfWork.Object,
            _tokenEscrow.Object,
            _chatOperations.Object,
            _notificationsSender.Object,
            _emailGenerationService.Object,
            _imageService.Object,
            folders
        );
    }
    private Mock<IDbContextTransaction> SetupTransaction()
    {
        var tx = new Mock<IDbContextTransaction>();

        tx.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        tx.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);

        _unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _chatOperations
            .Setup(x => x.CloseChatsForTradeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _chatOperations
            .Setup(x => x.PublishChatsClosedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return tx;
    }

    private static Trade ValidTradeInRealization()
    {
        return new Trade
        {
            ID = 1,
            Seller_ID = 1,
            Customer_ID = 2,
            MiddlemanUser_ID = 99,
            TradeStatus_ID = (int)TradeStatuses.InRealization,

            Offer = ValidOffer(),

            Customer = new User
            {
                ID = 2,
                Email = "buyer@test.com",
                ProfileInfo = new ProfileInfo
                {
                    Nickname = "Buyer"
                }
            },

            PostingUser = new User
            {
                ID = 1,
                Email = "seller@test.com",
                ProfileInfo = new ProfileInfo
                {
                    Nickname = "Seller"
                }
            },

            Rates = new List<Rate>()
        };
    }
    [Fact]
    public async Task AssignMiddlemanAsync_RequestNull()
    {
        var result = await _service.AssignMiddlemanAsync(
            null,
            "098712389",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Body is required.", result.Message);
    }

    [Fact]
    public async Task AssignMiddlemanAsync_InvalidId()
    {
        var result = await _service.AssignMiddlemanAsync(
            new AssignMiddlemanRequest(0),
            "78656870",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TradeId must be > 0.", result.Message);
    }

    [Fact]
    public async Task AssignMiddlemanAsync_WithoutMiddleman()
    {
        _userContext
            .Setup(x => x.GetRequiredUserAsync(
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("missing_sub_claim"));

        var result = await _service.AssignMiddlemanAsync(
            new AssignMiddlemanRequest(1),
            null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("missing_sub_claim", result.Message);
    }
    [Fact]
    public async Task SetTradeAsFailedAsync_RefundsBothSidesTokens()
    {
        SetupMiddleman(99);
        SetupTransaction();

        var trade = ValidTradeInRealization();

        trade.Offer.TokensOffered = 100;
        trade.Offer.TokensWanted = 200;

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferAndUsersDetailsByIdAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        _tokenEscrow
            .Setup(x => x.TryRefundEscrowToOtherAsync(
                2,
                1,
                100,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _tokenEscrow
            .Setup(x => x.TryRefundEscrowToOtherAsync(
                1,
                2,
                200,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _tokenEscrow
            .Setup(x => x.TryLockOwnTokensAsync(
                1,
                100,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.SetTradeAsFailedAsync(
            1,
            "auth0",
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        Assert.Equal(
            (int)TradeStatuses.Failed,
            trade.TradeStatus_ID);

        Assert.Equal(
            (int)OfferStatuses.Active,
            trade.Offer.OfferStatus_ID);

        _tokenEscrow.Verify(
            x => x.TryRefundEscrowToOtherAsync(
                2,
                1,
                100,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _tokenEscrow.Verify(
            x => x.TryRefundEscrowToOtherAsync(
                1,
                2,
                200,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task SetTradeAsRealisedAsync_ReleasesEscrowForBothSides()
    {
        SetupMiddleman(99);
        SetupTransaction();

        var trade = ValidTradeInRealization();

        trade.HasBuyersItems = true;
        trade.HasSellersItems = true;

        trade.Offer.TokensOffered = 100;
        trade.Offer.TokensWanted = 200;

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferAndUsersDetailsByIdAsync(
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        _tokenEscrow
            .Setup(x => x.TryReleaseOwnEscrowAsync(
                2,
                100,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _tokenEscrow
            .Setup(x => x.TryReleaseOwnEscrowAsync(
                1,
                200,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.SetTradeAsRealisedAsync(
            1,
            "auth0",
            ValidCompleteRequest(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        Assert.Equal(
            (int)TradeStatuses.SuccesfulRealization,
            trade.TradeStatus_ID);

        Assert.Equal(
            (int)OfferStatuses.Completed,
            trade.Offer.OfferStatus_ID);

        _tokenEscrow.Verify(
            x => x.TryReleaseOwnEscrowAsync(
                2,
                100,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _tokenEscrow.Verify(
            x => x.TryReleaseOwnEscrowAsync(
                1,
                200,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task AssignMiddlemanAsync_InvalidTrade()
    {
        SetupMiddleman();
        
        _tradeRepo
            .Setup(x => x.GetTradeWithOfferByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trade?)null);

        var result = await _service.AssignMiddlemanAsync(
            new AssignMiddlemanRequest(1),
            "896986",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade not found.", result.Message);
    }

    [Fact]
    public async Task AssignMiddlemanAsync_TradeInProgres()
    {
        SetupMiddleman();

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                Seller_ID = 10,
                Customer_ID = 20,
                TradeStatus_ID = (int)TradeStatuses.InRealization,
                Offer = ValidOffer()
            });

        var result = await _service.AssignMiddlemanAsync(
            new AssignMiddlemanRequest(1),
            "901238091",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade is not in NEW status.", result.Message);
    }

    [Fact]
    public async Task AssignMiddlemanAsync_HaveMiddleman()
    {
        SetupMiddleman();

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                Seller_ID = 1,
                Customer_ID = 2,
                MiddlemanUser_ID = 3,
                TradeStatus_ID = (int)TradeStatuses.New,
                Offer = ValidOffer()
            });

        var result = await _service.AssignMiddlemanAsync(
            new AssignMiddlemanRequest(1),
            "0907123",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade already has a middleman assigned.", result.Message);
    }

    [Fact]
    public async Task AssignMiddlemanAsync_MiddlemnaInTrade()
    {
        SetupMiddleman(id: 1);

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                Seller_ID = 1,
                Customer_ID = 2,
                TradeStatus_ID = (int)TradeStatuses.New,
                Offer = ValidOffer()
            });

        var result = await _service.AssignMiddlemanAsync(
            new AssignMiddlemanRequest(1),
            "899696890",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("You cannot assign yourself as middleman to your own trade.", result.Message);
    }

    [Fact]
    public async Task UpdateTradeByMiddlemanAsync_TradeWithoutMiddleman()
    {
        SetupMiddleman();

        _tradeRepo
            .Setup(x => x.GetByIdWithUrlsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                Seller_ID = 1,
                Customer_ID = 2,
                MiddlemanUser_ID = null,
                TradeStatus_ID = (int)TradeStatuses.InRealization
            });

        var result = await _service.UpdateTradeByMiddlemanAsync(
            1,
            new UpdateTradeRequest(true, true),
            "7867809867",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade has no middleman assigned.", result.Message);
    }

    [Fact]
    public async Task UpdateTradeByMiddlemanAsync_DifferentMiddleman()
    {
        SetupMiddleman(id: 99);

        _tradeRepo
            .Setup(x => x.GetByIdWithUrlsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                Seller_ID = 1,
                Customer_ID = 2,
                MiddlemanUser_ID = 3,
                TradeStatus_ID = (int)TradeStatuses.InRealization
            });

        var result = await _service.UpdateTradeByMiddlemanAsync(
            1,
            new UpdateTradeRequest(true, true),
            "129073",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("You are not assigned to this trade.", result.Message);
    }
    

    [Fact]
    public async Task UpdateTradeByMiddlemanAsync_InvalidStatus()
    {
        SetupMiddleman(id: 99);

        _tradeRepo
            .Setup(x => x.GetByIdWithUrlsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                Seller_ID = 1,
                Customer_ID = 2,
                MiddlemanUser_ID = 99,
                TradeStatus_ID = (int)TradeStatuses.New
            });

        var result = await _service.UpdateTradeByMiddlemanAsync(
            1,
            new UpdateTradeRequest(true, true),
            "1290381",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade is not in InRealization or Failed status.", result.Message);
    }

    [Fact]
    public async Task UpdateTradeByMiddlemanAsync_Fine()
    {
        SetupMiddleman(id: 99);

        var trade = new Trade
        {
            ID = 1,
            Seller_ID = 1,
            Customer_ID = 2,
            MiddlemanUser_ID = 99,
            TradeStatus_ID = (int)TradeStatuses.InRealization,
            HasBuyersItems = false,
            HasSellersItems = false
        };

        _tradeRepo
            .Setup(x => x.GetByIdWithUrlsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        _tradeRepo
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.UpdateTradeByMiddlemanAsync(
            1,
            new UpdateTradeRequest(true, true),
            "9018273",
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(trade.HasBuyersItems);
        Assert.True(trade.HasSellersItems);

        _tradeRepo.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetTradeAsFailedAsync_InvalidTradeID()
    {
        var result = await _service.SetTradeAsFailedAsync(
            0,
            "89123019273",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TradeId must be > 0.", result.Message);
    }

    [Fact]
    public async Task SetTradeAsFailedAsync_TradeDoesntExist()
    {
        SetupMiddleman();

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferAndUsersDetailsByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trade?)null);

        var result = await _service.SetTradeAsFailedAsync(
            1,
            "9012399012",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade not found.", result.Message);
    }

    [Fact]
    public async Task SetTradeAsFailedAsync_TradeNotInRealization()
    {
        SetupMiddleman(id: 99);

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferAndUsersDetailsByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                Seller_ID = 1,
                Customer_ID = 2,
                MiddlemanUser_ID = 99,
                TradeStatus_ID = (int)TradeStatuses.New,
                Offer = ValidOffer()
            });

        var result = await _service.SetTradeAsFailedAsync(
            1,
            "8192370192",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade is not in InRealization status.", result.Message);
    }

    [Fact]
    public async Task SetTradeAsRealisedAsync_TradeInvalid()
    {
        var result = await _service.SetTradeAsRealisedAsync(
            0,
            "89102739012",
            ValidCompleteRequest(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TradeId must be > 0.", result.Message);
    }

    [Fact]
    public async Task SetTradeAsRealisedAsync_TradeDoesntExist()
    {
        SetupMiddleman();

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferAndUsersDetailsByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trade?)null);

        var result = await _service.SetTradeAsRealisedAsync(
            1,
            "9812312",
            ValidCompleteRequest(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade not found.", result.Message);
    }

    [Fact]
    public async Task SetTradeAsRealisedAsync_TradeWithoutMiddleman()
    {
        SetupMiddleman();

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferAndUsersDetailsByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                Seller_ID = 1,
                Customer_ID = 2,
                MiddlemanUser_ID = null,
                TradeStatus_ID = (int)TradeStatuses.InRealization,
                Offer = ValidOffer()
            });

        var result = await _service.SetTradeAsRealisedAsync(
            1,
            "8127313",
            ValidCompleteRequest(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade has no middleman assigned.", result.Message);
    }

    [Fact]
    public async Task SetTradeAsRealisedAsyncNotAssignedMiddleman()
    {
        SetupMiddleman(id: 99);

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferAndUsersDetailsByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                Seller_ID = 1,
                Customer_ID = 2,
                MiddlemanUser_ID = 3,
                TradeStatus_ID = (int)TradeStatuses.InRealization,
                Offer = ValidOffer()
            });

        var result = await _service.SetTradeAsRealisedAsync(
            1,
            "891273913",
            ValidCompleteRequest(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("You are not assigned to this trade.", result.Message);
    }

    [Fact]
    public async Task SetTradeAsRealisedAsync_WrongItemsPossesion()
    {
        SetupMiddleman(id: 99);

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferAndUsersDetailsByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                Seller_ID = 1,
                Customer_ID = 2,
                MiddlemanUser_ID = 99,
                TradeStatus_ID = (int)TradeStatuses.InRealization,
                HasBuyersItems = true,
                HasSellersItems = false,
                Offer = ValidOffer()
            });

        var result = await _service.SetTradeAsRealisedAsync(
            1,
            "1283712093",
            ValidCompleteRequest(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Cannot set trade as realised as users items are still in your possession.", result.Message);
    }

    [Fact]
    public async Task GetByIdAsync_WInvalidTrade()
    {
        var result = await _service.GetByIdAsync(
            0,
            "1892763192",
            false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("trade id must be greater than 0", result.Message);
    }

    [Fact]
    public async Task GetByIdAsync_MissingUser()
    {
        _userContext
            .Setup(x => x.GetRequiredUserAsync(null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("missing_sub_claim"));

        var result = await _service.GetByIdAsync(
            1,
            null,
            false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("missing_sub_claim", result.Message);
    }

    [Fact]
    public async Task GetByIdAsync_TradeDoesntExist()
    {
        SetupUser();

        _listQuery
            .Setup(x => x.GetTradeByIdAsync(1, 1, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TradeListItemDTO?)null);

        var result = await _service.GetByIdAsync(
            1,
            "9012739701",
            false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("trade not found", result.Message);
    }

    [Fact]
    public async Task GetStatsAsync_FineUser()
    {
        SetupUser(id: 1);

        _tradeRepo
            .Setup(x => x.GetUserStatsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((All: 5, Completed: 2, MyActive: 1, Created: 3));

        var result = await _service.GetStatsAsync(
            "129382123",
            isMiddleman: false,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(5, result.Data.All);
        Assert.Equal(2, result.Data.Completed);
        Assert.Equal(1, result.Data.MyActive);
        Assert.Equal(3, result.Data.Created);
    }

    [Fact]
    public async Task GetStatsAsync_FineMiddleman()
    {
        SetupMiddleman(id: 99);

        _tradeRepo
            .Setup(x => x.GetMiddlemanStatsAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((All: 10, Completed: 4, MyActive: 2, Available: 6));

        var result = await _service.GetStatsAsync(
            "1290381203",
            isMiddleman: true,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(10, result.Data.All);
        Assert.Equal(4, result.Data.Completed);
        Assert.Equal(2, result.Data.MyActive);
        Assert.Equal(6, result.Data.Created);
    }

    [Fact]
    public async Task UploadTradeImageAsync_InvalidTrade()
    {
        var result = await _service.UploadTradeImageAsync(
            0,
            new UploadTradeImageRequest
            {
                Image = Mock.Of<IFormFile>(),
                IsBuyers = true
            },
            "1892732173",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TradeId must be > 0.", result.Message);
    }

    [Fact]
    public async Task UploadTradeImageAsync_NullImage()
    {
        var result = await _service.UploadTradeImageAsync(
            1,
            new UploadTradeImageRequest
            {
                Image = null!,
                IsBuyers = true
            },
            "12893712",
            CancellationToken.None);

        Assert.False(result.IsSuccess, result.Message);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task UploadTradeImageAsync_TradeDoesntExist()
    {
        SetupMiddleman(id: 99);

        _tradeRepo
            .Setup(x => x.GetByIdWithUrlsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trade?)null);

        var result = await _service.UploadTradeImageAsync(
            1,
            new UploadTradeImageRequest
            {
                Image = Mock.Of<IFormFile>(),
                IsBuyers = true
            },
            "128973123",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade not found.", result.Message);
    }

    private void SetupUser(int id = 1)
    {
        _userContext
            .Setup(x => x.GetRequiredUserAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = id,
                Email = "user@test.com",
                Tokens = 1000,
                ProfileInfo = new ProfileInfo()
                {
                    Nickname = "User"
                }
            });
    }

    private void SetupMiddleman(int id = 99)
    {
        _userContext
            .Setup(x => x.GetRequiredUserAsync(
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = id,
                Email = "middleman@test.com",
                Tokens = 10000,
                ProfileInfo = new ProfileInfo
                {
                    Nickname = "Middleman"
                }
            });
    }

    private static Offer ValidOffer()
    {
        return new Offer
        {
            ID = 1,
            Title = "Test offer",
            User_ID = 10,
            OfferStatus_ID = (int)OfferStatuses.Active,
            TokensOffered = 0,
            TokensWanted = 0,
            ExpDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
        };
    }

    private static CompleteAndMarkTradeRequest ValidCompleteRequest()
    {
        return new CompleteAndMarkTradeRequest(
            BuyersGrade: 5,
            BuyersDescription: "ok",
            SellersGrade: 5,
            SellersDescription: "ok"
        );
    }
}