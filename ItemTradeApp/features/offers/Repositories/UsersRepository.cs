using ItemTradeApp.Features.Offers.DTOs.RequestDTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Offers.Repositories;

public sealed record UserState(int Id, bool IsDeleted, int Tokens);

public interface IUsersRepository
{
    Task<UserState?> GetStateByAuth0IdAsync(string auth0UserId, CancellationToken ct = default);
    Task<bool> TrySubtractTokenCostAsync(int userId, int tokenCost, CancellationToken ct = default);
    
    Task<UserNotificationData?> GetNotificationDataByIdAsync(
        int userId,
        CancellationToken ct = default);

    Task<UserNotificationData?> GetNotificationDataByAuth0IdAsync(
        string auth0UserId,
        CancellationToken ct = default);

}

public class UsersRepository(AppDbContext dbContext) : IUsersRepository
{
    public Task<UserState?> GetStateByAuth0IdAsync(string auth0UserId, CancellationToken ct = default)
        => dbContext.Users.AsNoTracking()
            .Where(u => u.Auth0UserID == auth0UserId)
            .Select(u => new UserState(u.ID, u.IsDeleted, u.Tokens))
            .SingleOrDefaultAsync(ct);

    public async Task<bool> TrySubtractTokenCostAsync(int userId, int tokenCost, CancellationToken ct = default)
    {
        var updated = await dbContext.Users
            .Where(u => u.ID == userId && !u.IsDeleted && u.Tokens >= tokenCost)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Tokens, u => u.Tokens - tokenCost), ct);
        return updated == 1;
    }
    public Task<UserNotificationData?> GetNotificationDataByIdAsync(
        int userId,
        CancellationToken ct = default)
        => dbContext.Users
            .AsNoTracking()
            .Where(u => u.ID == userId)
            .Select(u => new UserNotificationData(
                u.ID,
                u.Email,
                u.ProfileInfo != null
                    ? u.ProfileInfo.Nickname
                    : null
            ))
            .SingleOrDefaultAsync(ct);

    public Task<UserNotificationData?> GetNotificationDataByAuth0IdAsync(
        string auth0UserId,
        CancellationToken ct = default)
        => dbContext.Users
            .AsNoTracking()
            .Where(u => u.Auth0UserID == auth0UserId)
            .Select(u => new UserNotificationData(
                u.ID,
                u.Email,
                u.ProfileInfo != null
                    ? u.ProfileInfo.Nickname
                    : null
            ))
            .SingleOrDefaultAsync(ct);
}