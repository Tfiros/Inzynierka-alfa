using ItemTradeApp.Features.Offers;
using ItemTradeApp.Features.Offers.Repositories;
using ItemTradeApp.Features.Shared.Notifications;
using ItemTradeApp.Features.Shared.TokenEscrow;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace ItemTradeApp.UnitTests.Features.Offers;

[TestSubject(typeof(OfferExpirationService))]
public class OfferExpirationServiceTest
{
    private readonly Mock<IOffersRepository> _offersRepo = new();
    private readonly Mock<ICounterOfferRepository> _counterOffersRepo = new();
    private readonly Mock<ITokenEscrow> _tokenEscrow = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<INotificationSender> _notificationSender = new();

    private readonly OfferExpirationService _offerExpirationService;


    public OfferExpirationServiceTest()
    {
        _offerExpirationService = new OfferExpirationService(
            _offersRepo.Object,
            _counterOffersRepo.Object,
            _tokenEscrow.Object,
            _uow.Object,
            _notificationSender.Object);
    }

    private static Offer ExpiredOffer() => new Offer()
    {
        ID = 7,
        User_ID = 1,
        TokensOffered = 50,
        Title = "TestOffer",
        OfferStatus_ID = (int)OfferStatuses.Active
    };
    
    private static CounterOffer PendingCounterOffer() => new CounterOffer()
    {
        ID = 11,
        User_ID = 99,
        TokensOffered = 30,
        CounterOfferStatus_Id = (int)CounterOfferStatuses.Pending,
        Offer_Id = 7
    };



    [Fact]
    public async Task ExpireOverdueOffersAsync_WhenValidWithPendingCounterOffer_ReleasesDeniesExpires()
    {
        var tx = new Mock<IDbContextTransaction>();
        var co = PendingCounterOffer();
        var offer = ExpiredOffer();
        _offersRepo.Setup(x => x.GetActiveExpiredOffersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Offer>() { offer });
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _tokenEscrow.Setup(x =>
                x.TryReleaseOwnEscrowAsync(offer.User_ID, offer.TokensOffered, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _counterOffersRepo.Setup(x => x.GetAllPendingForOfferAsync(offer.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CounterOffer>() { co });
        _tokenEscrow.Setup(x =>
                x.TryReleaseOwnEscrowAsync(co.User_ID, co.TokensOffered, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _counterOffersRepo.Setup(x => x.DenyAsync(co.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _offersRepo.Setup(x => x.SetOfferExpiredAsync(offer.ID,It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var count = await _offerExpirationService.ExpireOverdueOffersAsync();
        Assert.Equal(1, count);
        _tokenEscrow.Verify(x => x.TryReleaseOwnEscrowAsync(offer.User_ID, offer.TokensOffered, It.IsAny<CancellationToken>()), Times.Once);
        _tokenEscrow.Verify(x => x.TryReleaseOwnEscrowAsync(co.User_ID, co.TokensOffered, It.IsAny<CancellationToken>()), Times.Once);
        _counterOffersRepo.Verify(x => x.DenyAsync(co.ID, It.IsAny<CancellationToken>()), Times.Once);
        _offersRepo.Verify(x => x.SetOfferExpiredAsync(offer.ID, It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task ExpireOverdueOffersAsync_WhenCounterOfferDenyFails_RollbacksAndDoesNotExpire()
    {
        var tx = new Mock<IDbContextTransaction>();
        var co = PendingCounterOffer();
        var offer = ExpiredOffer();
        _offersRepo.Setup(x => x.GetActiveExpiredOffersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Offer>() { offer });
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _tokenEscrow.Setup(x =>
                x.TryReleaseOwnEscrowAsync(offer.User_ID, offer.TokensOffered, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _counterOffersRepo.Setup(x => x.GetAllPendingForOfferAsync(offer.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CounterOffer>() { co });
        _tokenEscrow.Setup(x =>
                x.TryReleaseOwnEscrowAsync(co.User_ID, co.TokensOffered, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _counterOffersRepo.Setup(x => x.DenyAsync(co.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var count = await _offerExpirationService.ExpireOverdueOffersAsync();
        Assert.Equal(0, count);
        _tokenEscrow.Verify(x => x.TryReleaseOwnEscrowAsync(offer.User_ID, offer.TokensOffered, It.IsAny<CancellationToken>()), Times.Once);
        _tokenEscrow.Verify(x => x.TryReleaseOwnEscrowAsync(co.User_ID, co.TokensOffered, It.IsAny<CancellationToken>()), Times.Once);
        _counterOffersRepo.Verify(x => x.DenyAsync(co.ID, It.IsAny<CancellationToken>()), Times.Once);
        _offersRepo.Verify(x => x.SetOfferExpiredAsync(offer.ID, It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task ExpireOverdueOffersAsync_WhenCounterOfferEscrowFails_RollbacksAndDoesNotExpire()
    {
        var tx = new Mock<IDbContextTransaction>();
        var co = PendingCounterOffer();
        var offer = ExpiredOffer();
        _offersRepo.Setup(x => x.GetActiveExpiredOffersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Offer>() { offer });
        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _tokenEscrow.Setup(x =>
                x.TryReleaseOwnEscrowAsync(offer.User_ID, offer.TokensOffered, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _counterOffersRepo.Setup(x => x.GetAllPendingForOfferAsync(offer.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CounterOffer>() { co });
        _tokenEscrow.Setup(x =>
                x.TryReleaseOwnEscrowAsync(co.User_ID, co.TokensOffered, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _counterOffersRepo.Setup(x => x.DenyAsync(co.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var count = await _offerExpirationService.ExpireOverdueOffersAsync();
        Assert.Equal(0, count);
        _tokenEscrow.Verify(x => x.TryReleaseOwnEscrowAsync(offer.User_ID, offer.TokensOffered, It.IsAny<CancellationToken>()), Times.Once);
        _tokenEscrow.Verify(x => x.TryReleaseOwnEscrowAsync(co.User_ID, co.TokensOffered, It.IsAny<CancellationToken>()), Times.Once);
        _counterOffersRepo.Verify(x => x.DenyAsync(co.ID, It.IsAny<CancellationToken>()), Times.Never);
        _offersRepo.Verify(x => x.SetOfferExpiredAsync(offer.ID, It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}