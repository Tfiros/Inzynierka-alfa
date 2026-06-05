using ItemTradeApp.Features.Offers.Repositories;
using ItemTradeApp.Features.Shared.Notifications;
using ItemTradeApp.Features.Shared.TokenEscrow;
using ItemTradeApp.Persistence;
using ItemTradeApp.Resources.NotificationsTemplates;

namespace ItemTradeApp.Features.Offers;

public interface IOfferExpirationService
{
    Task<int> ExpireOverdueOffersAsync(CancellationToken ct = default);
}

public sealed class OfferExpirationService(
    IOffersRepository offersRepository,
    ICounterOfferRepository counterOfferRepository,
    ITokenEscrow tokenEscrow,
    IUnitOfWork unitOfWork,
    INotificationSender notificationSender) : IOfferExpirationService
{
    public async Task<int> ExpireOverdueOffersAsync(CancellationToken ct = default)
    {
        var expiredOffers = await offersRepository.GetActiveExpiredOffersAsync(ct);
        var count = 0;
        foreach (var expiredOffer in expiredOffers)
        {
            await using var tx = await unitOfWork.BeginTransactionAsync(ct);
            try
            {
                if (expiredOffer.TokensOffered > 0)
                {
                    var released =
                        await tokenEscrow.TryReleaseOwnEscrowAsync(expiredOffer.User_ID, expiredOffer.TokensOffered,
                            ct);
                    if (!released)
                    {
                        await tx.RollbackAsync(ct);
                        continue;
                    }
                }

                var pendingCounterOffers = await counterOfferRepository.GetAllPendingForOfferAsync(expiredOffer.ID, ct);
                foreach (var counterOffer in pendingCounterOffers)
                {
                    if (counterOffer.TokensOffered > 0)
                    {
                        await tokenEscrow.TryReleaseOwnEscrowAsync(counterOffer.User_ID, counterOffer.TokensOffered, ct);
                    }

                    counterOffer.CounterOfferStatus_Id = (int)CounterOfferStatuses.Denied;
                }

                var expired = await offersRepository.SetOfferExpiredAsync(expiredOffer.ID, ct);
                if (!expired)
                {
                    await tx.RollbackAsync(ct);
                    continue;
                }

                await unitOfWork.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                count++;

                try
                {
                    await notificationSender.SendAsync(expiredOffer.User_ID,
                        NotificationsMessages.OfferExpired(expiredOffer.Title), ct);
                    foreach (var counterOffer in pendingCounterOffers)
                    {
                        await notificationSender.SendAsync(counterOffer.User_ID,
                            NotificationsMessages.CounterOfferDenied(expiredOffer.Title), ct);
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            catch
            {
                await tx.RollbackAsync(ct);
            }
        }
        return count;
    }
}