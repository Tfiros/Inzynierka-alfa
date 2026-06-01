using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Shared.Chat;
using ItemTradeApp.Features.Shared.DTOs;
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
        _validator
            .Setup(x => x.Normalize(It.IsAny<int>(), It.IsAny<int>()))
            .Returns((int page, int pageSize) => (page <= 0 ? 1 : page, pageSize <= 0 ? 10 : pageSize));

        _validator
            .Setup(x => x.ValidateTradesQuery(It.IsAny<TradesQuery?>(), It.IsAny<TradeStatuses>()))
            .Returns((Result<PagedResponse<TradeListItemDTO>>?)null);

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

    [Fact]
    public async Task AssignMiddlemanAsync_WhenRequestIsNull_ReturnsBadRequest()
    {
        var result = await _service.AssignMiddlemanAsync(
            null,
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Body is required.", result.Message);
    }

    [Fact]
    public async Task AssignMiddlemanAsync_WhenTradeIdIsInvalid_ReturnsBadRequest()
    {
        var result = await _service.AssignMiddlemanAsync(
            new AssignMiddlemanRequest(0),
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TradeId must be > 0.", result.Message);
    }

    [Fact]
    public async Task AssignMiddlemanAsync_WhenMiddlemanIsMissing_ReturnsUnauthorized()
    {
        _userContext
            .Setup(x => x.GetRequiredMiddlemanAsync(null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("missing_sub_claim"));

        var result = await _service.AssignMiddlemanAsync(
            new AssignMiddlemanRequest(1),
            null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("missing_sub_claim", result.Message);
    }

    [Fact]
    public async Task AssignMiddlemanAsync_WhenTradeDoesNotExist_ReturnsNotFound()
    {
        SetupMiddleman();

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trade?)null);

        var result = await _service.AssignMiddlemanAsync(
            new AssignMiddlemanRequest(1),
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade not found.", result.Message);
    }

    [Fact]
    public async Task AssignMiddlemanAsync_WhenTradeIsNotNew_ReturnsBadRequest()
    {
        SetupMiddleman();

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                User_ID = 10,
                Customer_ID = 20,
                TradeStatus_ID = (int)TradeStatuses.InRealization,
                Offer = ValidOffer()
            });

        var result = await _service.AssignMiddlemanAsync(
            new AssignMiddlemanRequest(1),
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade is not in NEW status.", result.Message);
    }

    [Fact]
    public async Task AssignMiddlemanAsync_WhenTradeAlreadyHasMiddleman_ReturnsConflict()
    {
        SetupMiddleman();

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                User_ID = 10,
                Customer_ID = 20,
                MiddlemanUser_ID = 99,
                TradeStatus_ID = (int)TradeStatuses.New,
                Offer = ValidOffer()
            });

        var result = await _service.AssignMiddlemanAsync(
            new AssignMiddlemanRequest(1),
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade already has a middleman assigned.", result.Message);
    }

    [Fact]
    public async Task AssignMiddlemanAsync_WhenMiddlemanIsInTrade_ReturnsForbidden()
    {
        SetupMiddleman(id: 10);

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                User_ID = 10,
                Customer_ID = 20,
                TradeStatus_ID = (int)TradeStatuses.New,
                Offer = ValidOffer()
            });

        var result = await _service.AssignMiddlemanAsync(
            new AssignMiddlemanRequest(1),
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("You cannot assign yourself as middleman to your own trade.", result.Message);
    }

    [Fact]
    public async Task UpdateTradeByMiddlemanAsync_WhenTradeIdIsInvalid_ReturnsBadRequest()
    {
        var result = await _service.UpdateTradeByMiddlemanAsync(
            0,
            new UpdateTradeRequest(true, true),
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TradeId must be > 0.", result.Message);
    }

    [Fact]
    public async Task UpdateTradeByMiddlemanAsync_WhenRequestIsNull_ReturnsBadRequest()
    {
        var result = await _service.UpdateTradeByMiddlemanAsync(
            1,
            null,
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Body is required.", result.Message);
    }

    [Fact]
    public async Task UpdateTradeByMiddlemanAsync_WhenTradeDoesNotExist_ReturnsNotFound()
    {
        SetupMiddleman();

        _tradeRepo
            .Setup(x => x.GetByIdWithUrlsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trade?)null);

        var result = await _service.UpdateTradeByMiddlemanAsync(
            1,
            new UpdateTradeRequest(true, true),
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade not found.", result.Message);
    }

    [Fact]
    public async Task UpdateTradeByMiddlemanAsync_WhenTradeHasNoMiddleman_ReturnsBadRequest()
    {
        SetupMiddleman();

        _tradeRepo
            .Setup(x => x.GetByIdWithUrlsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                User_ID = 10,
                Customer_ID = 20,
                MiddlemanUser_ID = null,
                TradeStatus_ID = (int)TradeStatuses.InRealization
            });

        var result = await _service.UpdateTradeByMiddlemanAsync(
            1,
            new UpdateTradeRequest(true, true),
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade has no middleman assigned.", result.Message);
    }

    [Fact]
    public async Task UpdateTradeByMiddlemanAsync_WhenUserIsNotAssignedMiddleman_ReturnsForbidden()
    {
        SetupMiddleman(id: 99);

        _tradeRepo
            .Setup(x => x.GetByIdWithUrlsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                User_ID = 10,
                Customer_ID = 20,
                MiddlemanUser_ID = 50,
                TradeStatus_ID = (int)TradeStatuses.InRealization
            });

        var result = await _service.UpdateTradeByMiddlemanAsync(
            1,
            new UpdateTradeRequest(true, true),
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("You are not assigned to this trade.", result.Message);
    }

    [Fact]
    public async Task UpdateTradeByMiddlemanAsync_WhenMiddlemanIsInTrade_ReturnsForbidden()
    {
        SetupMiddleman(id: 10);

        _tradeRepo
            .Setup(x => x.GetByIdWithUrlsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                User_ID = 10,
                Customer_ID = 20,
                MiddlemanUser_ID = 10,
                TradeStatus_ID = (int)TradeStatuses.InRealization
            });

        var result = await _service.UpdateTradeByMiddlemanAsync(
            1,
            new UpdateTradeRequest(true, true),
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("You cannot manage your own trade as middleman.", result.Message);
    }

    [Fact]
    public async Task UpdateTradeByMiddlemanAsync_WhenTradeStatusIsInvalid_ReturnsBadRequest()
    {
        SetupMiddleman(id: 99);

        _tradeRepo
            .Setup(x => x.GetByIdWithUrlsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                User_ID = 10,
                Customer_ID = 20,
                MiddlemanUser_ID = 99,
                TradeStatus_ID = (int)TradeStatuses.New
            });

        var result = await _service.UpdateTradeByMiddlemanAsync(
            1,
            new UpdateTradeRequest(true, true),
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade is not in InRealization or Failed status.", result.Message);
    }

    [Fact]
    public async Task UpdateTradeByMiddlemanAsync_WhenRequestIsValid_UpdatesTrade()
    {
        SetupMiddleman(id: 99);

        var trade = new Trade
        {
            ID = 1,
            User_ID = 10,
            Customer_ID = 20,
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
            "auth0|middleman",
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(trade.HasBuyersItems);
        Assert.True(trade.HasSellersItems);

        _tradeRepo.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SetTradeAsFailedAsync_WhenTradeIdIsInvalid_ReturnsBadRequest()
    {
        var result = await _service.SetTradeAsFailedAsync(
            0,
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TradeId must be > 0.", result.Message);
    }

    [Fact]
    public async Task SetTradeAsFailedAsync_WhenTradeDoesNotExist_ReturnsNotFound()
    {
        SetupMiddleman();

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferAndUsersDetailsByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trade?)null);

        var result = await _service.SetTradeAsFailedAsync(
            1,
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade not found.", result.Message);
    }

    [Fact]
    public async Task SetTradeAsFailedAsync_WhenTradeHasNoMiddleman_ReturnsBadRequest()
    {
        SetupMiddleman();

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferAndUsersDetailsByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                User_ID = 10,
                Customer_ID = 20,
                MiddlemanUser_ID = null,
                TradeStatus_ID = (int)TradeStatuses.InRealization,
                Offer = ValidOffer()
            });

        var result = await _service.SetTradeAsFailedAsync(
            1,
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade has no middleman assigned.", result.Message);
    }

    [Fact]
    public async Task SetTradeAsFailedAsync_WhenUserIsNotAssignedMiddleman_ReturnsForbidden()
    {
        SetupMiddleman(id: 99);

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferAndUsersDetailsByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                User_ID = 10,
                Customer_ID = 20,
                MiddlemanUser_ID = 50,
                TradeStatus_ID = (int)TradeStatuses.InRealization,
                Offer = ValidOffer()
            });

        var result = await _service.SetTradeAsFailedAsync(
            1,
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("You are not assigned to this trade.", result.Message);
    }

    [Fact]
    public async Task SetTradeAsFailedAsync_WhenTradeIsNotInRealization_ReturnsBadRequest()
    {
        SetupMiddleman(id: 99);

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferAndUsersDetailsByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                User_ID = 10,
                Customer_ID = 20,
                MiddlemanUser_ID = 99,
                TradeStatus_ID = (int)TradeStatuses.New,
                Offer = ValidOffer()
            });

        var result = await _service.SetTradeAsFailedAsync(
            1,
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade is not in InRealization status.", result.Message);
    }

    [Fact]
    public async Task SetTradeAsRealisedAsync_WhenTradeIdIsInvalid_ReturnsBadRequest()
    {
        var result = await _service.SetTradeAsRealisedAsync(
            0,
            "auth0|middleman",
            ValidCompleteRequest(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TradeId must be > 0.", result.Message);
    }

    [Fact]
    public async Task SetTradeAsRealisedAsync_WhenTradeDoesNotExist_ReturnsNotFound()
    {
        SetupMiddleman();

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferAndUsersDetailsByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Trade?)null);

        var result = await _service.SetTradeAsRealisedAsync(
            1,
            "auth0|middleman",
            ValidCompleteRequest(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade not found.", result.Message);
    }

    [Fact]
    public async Task SetTradeAsRealisedAsync_WhenTradeHasNoMiddleman_ReturnsBadRequest()
    {
        SetupMiddleman();

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferAndUsersDetailsByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                User_ID = 10,
                Customer_ID = 20,
                MiddlemanUser_ID = null,
                TradeStatus_ID = (int)TradeStatuses.InRealization,
                Offer = ValidOffer()
            });

        var result = await _service.SetTradeAsRealisedAsync(
            1,
            "auth0|middleman",
            ValidCompleteRequest(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade has no middleman assigned.", result.Message);
    }

    [Fact]
    public async Task SetTradeAsRealisedAsync_WhenUserIsNotAssignedMiddleman_ReturnsForbidden()
    {
        SetupMiddleman(id: 99);

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferAndUsersDetailsByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                User_ID = 10,
                Customer_ID = 20,
                MiddlemanUser_ID = 50,
                TradeStatus_ID = (int)TradeStatuses.InRealization,
                Offer = ValidOffer()
            });

        var result = await _service.SetTradeAsRealisedAsync(
            1,
            "auth0|middleman",
            ValidCompleteRequest(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("You are not assigned to this trade.", result.Message);
    }

    [Fact]
    public async Task SetTradeAsRealisedAsync_WhenUsersItemsAreStillInMiddlemanPossession_ReturnsForbidden()
    {
        SetupMiddleman(id: 99);

        _tradeRepo
            .Setup(x => x.GetTradeWithOfferAndUsersDetailsByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                User_ID = 10,
                Customer_ID = 20,
                MiddlemanUser_ID = 99,
                TradeStatus_ID = (int)TradeStatuses.InRealization,
                HasBuyersItems = true,
                HasSellersItems = false,
                Offer = ValidOffer()
            });

        var result = await _service.SetTradeAsRealisedAsync(
            1,
            "auth0|middleman",
            ValidCompleteRequest(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Cannot set trade as realised as users items are still in your possession.", result.Message);
    }

    [Fact]
    public async Task GetByIdAsync_WhenTradeIdIsInvalid_ReturnsBadRequest()
    {
        var result = await _service.GetByIdAsync(
            0,
            "auth0|user",
            false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("trade id must be greater than 0", result.Message);
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserIsMissing_ReturnsUnauthorized()
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
    public async Task GetByIdAsync_WhenTradeDoesNotExist_ReturnsNotFound()
    {
        SetupUser();

        _listQuery
            .Setup(x => x.GetTradeByIdAsync(1, 1, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TradeListItemDTO?)null);

        var result = await _service.GetByIdAsync(
            1,
            "auth0|user",
            false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("trade not found", result.Message);
    }

    [Fact]
    public async Task GetStatsAsync_WhenUserView_ReturnsSuccess()
    {
        SetupUser(id: 1);

        _tradeRepo
            .Setup(x => x.GetUserStatsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((All: 5, Completed: 2, MyActive: 1, Created: 3));

        var result = await _service.GetStatsAsync(
            "auth0|user",
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
    public async Task GetStatsAsync_WhenMiddlemanView_ReturnsSuccess()
    {
        SetupMiddleman(id: 99);

        _tradeRepo
            .Setup(x => x.GetMiddlemanStatsAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((All: 10, Completed: 4, MyActive: 2, Available: 6));

        var result = await _service.GetStatsAsync(
            "auth0|middleman",
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
    public async Task GetAvailableNewAsync_WhenUserIsMissing_ReturnsUnauthorized()
    {
        _userContext
            .Setup(x => x.GetRequiredUserAsync(null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("missing_sub_claim"));

        var result = await _service.GetAvailableNewAsync(
            1,
            10,
            null,
            null,
            false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("missing_sub_claim", result.Message);
    }

    [Fact]
    public async Task GetAvailableNewAsync_WhenRequestIsValid_ReturnsPagedResponse()
    {
        SetupUser(id: 1);

        _listQuery
            .Setup(x => x.GetTradesAsync(
                TradeStatuses.New,
                1,
                10,
                1,
                It.IsAny<TradesQuery>(),
                false,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<TradeListItemDTO>(), 0));

        var result = await _service.GetAvailableNewAsync(
            1,
            10,
            null,
            "auth0|user",
            false,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(10, result.Data.PageSize);
        Assert.Equal(0, result.Data.TotalCount);
    }

    [Fact]
    public async Task UploadTradeImageAsync_WhenTradeIdIsInvalid_ReturnsBadRequest()
    {
        var result = await _service.UploadTradeImageAsync(
            0,
            new UploadTradeImageRequest
            {
                Image = Mock.Of<IFormFile>(),
                IsBuyers = true
            },
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TradeId must be > 0.", result.Message);
    }

    [Fact]
    public async Task UploadTradeImageAsync_WhenImageIsNull_ReturnsBadRequest()
    {
        var result = await _service.UploadTradeImageAsync(
            1,
            new UploadTradeImageRequest
            {
                Image = null!,
                IsBuyers = true
            },
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess, result.Message);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task UploadTradeImageAsync_WhenTradeDoesNotExist_ReturnsNotFound()
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
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade not found.", result.Message);
    }

    [Fact]
    public async Task UploadTradeImageAsync_WhenUserIsNotAssignedMiddleman_ReturnsForbidden()
    {
        SetupMiddleman(id: 99);

        _tradeRepo
            .Setup(x => x.GetByIdWithUrlsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                User_ID = 10,
                Customer_ID = 20,
                MiddlemanUser_ID = 50,
                TradeStatus_ID = (int)TradeStatuses.InRealization,
                Urls = new List<TradeUrl>()
            });

        var result = await _service.UploadTradeImageAsync(
            1,
            new UploadTradeImageRequest
            {
                Image = Mock.Of<IFormFile>(),
                IsBuyers = true
            },
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("You are not assigned to this trade.", result.Message);
    }

    [Fact]
    public async Task UploadTradeImageAsync_WhenTradeIsNotInRealization_ReturnsBadRequest()
    {
        SetupMiddleman(id: 99);

        _tradeRepo
            .Setup(x => x.GetByIdWithUrlsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Trade
            {
                ID = 1,
                User_ID = 10,
                Customer_ID = 20,
                MiddlemanUser_ID = 99,
                TradeStatus_ID = (int)TradeStatuses.New,
                Urls = new List<TradeUrl>()
            });

        var result = await _service.UploadTradeImageAsync(
            1,
            new UploadTradeImageRequest
            {
                Image = Mock.Of<IFormFile>(),
                IsBuyers = true
            },
            "auth0|middleman",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trade is not in realization status.", result.Message);
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
            .Setup(x => x.GetRequiredMiddlemanAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                ID = id,
                Email = "middleman@test.com",
                Tokens = 1000,
                ProfileInfo = new ProfileInfo()
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