using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Users.UserInfo;

public interface IUserInfoRepository
{
    Task<int> GetChatUnreadTotalAsync(int userId, CancellationToken ct);
    Task<User?> GetUserWithProfileInfoByUserIdAsync(int id, CancellationToken ct);
    Task<User?> GetUserWithProfileByAuth0IdAsync(string authZeroUserId, CancellationToken ct);
    Task UpdateUserWithProfileInfoAsync(ProfileInfo profile, CancellationToken ct);
    Task<bool> ExistsByAuth0IdAsync(string auth0UserId, CancellationToken ct);
    Task<bool> ExistsByIdAsync(int auth0UserId, CancellationToken ct);

    Task<(int activeOffersCount, int successTradeCount, int completedTradeCount, float rating )?>
        GetUserStatsByUserIdAsync(int id, CancellationToken ct);
}
public class UserInfoRepository(AppDbContext dbContext) : IUserInfoRepository
{
    public async Task<bool> ExistsByAuth0IdAsync(string auth0UserId, CancellationToken ct) =>
       await dbContext.Users.AnyAsync(u => u.Auth0UserID == auth0UserId, ct);
    public async Task<User?> GetUserWithProfileInfoByUserIdAsync(int id, CancellationToken ct)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .Include(u => u.ProfileInfo)
            .Include(u => u.Chats)
            .ThenInclude(c => c.ChatConversation)
            .SingleOrDefaultAsync(u => u.ID == id, ct);
        return user;
    }
    public async Task<int> GetChatUnreadTotalAsync(int userId, CancellationToken ct)
    {
        var q =
            from cm in dbContext.ConversationMembers.AsNoTracking()
            where cm.UserId == userId
            where dbContext.ChatMessages.AsNoTracking().Any(m =>
                m.ChatConversationId == cm.ChatConversationId &&
                m.DeletedAt == null &&
                m.SenderId != userId &&
                (cm.LastReadMessageId == null || m.Id > cm.LastReadMessageId)
            )
            select 1;

        return await q.CountAsync(ct);
    }
    public async Task<(int activeOffersCount, int successTradeCount, int completedTradeCount, float rating )?> GetUserStatsByUserIdAsync(int id, CancellationToken ct)
    {
        var res = await dbContext.Users.AsNoTracking().Where(u => u.ID == id && !u.IsDeleted)
            .Select(u => new
            {
                ActiveOffers = u.Offers.Count(o => o.OfferStatus_ID == (int)OfferStatuses.Active),
                SuccessTrade = u.OwningTrades.Count(t => t.TradeStatus_ID == (int)TradeStatuses.SuccesfulRealization),
                CompletedTrade = u.OwningTrades.Count(t =>
                    t.TradeStatus_ID == (int)TradeStatuses.SuccesfulRealization ||
                    t.TradeStatus_ID == (int)TradeStatuses.Failed),
                Rating = u.Rates.Select(r => (decimal?)r.Mark).Average() ?? 0m

            }).SingleOrDefaultAsync(ct);
        if (res is null) return null;
        return (res.ActiveOffers,res.SuccessTrade,res.CompletedTrade,(float)res.Rating);
    }

    public async Task<User?> GetUserWithProfileByAuth0IdAsync(string authZeroUserId, CancellationToken ct)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .Include(u => u.ProfileInfo)
            .SingleOrDefaultAsync(u => u.Auth0UserID == authZeroUserId, ct);
    }

    public async Task UpdateUserWithProfileInfoAsync(ProfileInfo profile, CancellationToken ct)
    {
        dbContext.ProfileInfos.Update(profile);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsByIdAsync(int id, CancellationToken ct) =>
        await dbContext.Users.AnyAsync(u => u.ID == id && !u.IsDeleted, ct);
}