using ItemTradeApp.Features.Users.UserManagement.DTOs.Request;
using ItemTradeApp.Features.Users.UserManagement.DTOs.Response;
using ItemTradeApp.Features.Users.UserManagement.Enums;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.Users.UserManagement;

public interface IUserManagementRepository
{
    Task<User?> GetUserByAuth0IdAsync(string auth0UserId, CancellationToken ct = default);

    Task<int> UpdateOfferStatusesForUserAsync(
        int userId,
        IReadOnlyCollection<int> currentStatuses,
        int newStatus,
        CancellationToken ct = default);

    Task<int> UpdateTradeStatusesForUserAsync(
        int userId,
        IReadOnlyCollection<int> currentStatuses,
        int newStatus,
        CancellationToken ct = default);

    Task<int> UpdateCounterOfferStatusesForUserAsync(
        int userId,
        int newStatus,
        CancellationToken ct = default);

    void SoftDeleteUser(User user);

    Task<(List<UserListItemDTO> Items, int TotalCount, int RegisteredLastMonthCount, int MiddlemenCount, int TotalUsers)>
        GetUsersPageWithStatsAsync(
            UserListQuery query,
            IReadOnlyCollection<string>? auth0IdFilter = null,
            IReadOnlyCollection<string>? middlemanAuth0Ids = null,
            CancellationToken ct = default);
}

public class UserManagementRepository(AppDbContext dbContext) : IUserManagementRepository
{
    public async Task<User?> GetUserByAuth0IdAsync(string auth0UserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return null;

        return await dbContext.Users
            .Include(u => u.ProfileInfo)
            .SingleOrDefaultAsync(u => u.Auth0UserID == auth0UserId && !u.IsDeleted, ct);
    }

    public Task<int> UpdateOfferStatusesForUserAsync(
        int userId,
        IReadOnlyCollection<int> currentStatuses,
        int newStatus,
        CancellationToken ct = default)
    {
        return dbContext.Offers
            .Where(o =>
                o.User_ID == userId &&
                currentStatuses.Contains(o.OfferStatus_ID))
            .ExecuteUpdateAsync(
                s => s.SetProperty(o => o.OfferStatus_ID, newStatus),
                ct);
    }

    public Task<int> UpdateTradeStatusesForUserAsync(
        int userId,
        IReadOnlyCollection<int> currentStatuses,
        int newStatus,
        CancellationToken ct = default)
    {
        return dbContext.Trades
            .Where(t =>
                (t.Customer_ID == userId ||
                 t.User_ID == userId ||
                 t.MiddlemanUser_ID == userId) &&
                currentStatuses.Contains(t.TradeStatus_ID))
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.TradeStatus_ID, newStatus),
                ct);
    }

    public Task<int> UpdateCounterOfferStatusesForUserAsync(
        int userId,
        int newStatus,
        CancellationToken ct = default)
    {
        return dbContext.CounterOffers
            .Where(co => co.User_ID == userId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(co => co.CounterOfferStatus_Id, newStatus),
                ct);
    }

    public void SoftDeleteUser(User user)
    {
        user.IsDeleted = true;
        user.Auth0UserID = null;
        user.StripeCustomerID = null;
        user.Email = null;
    }

    public async Task<(List<UserListItemDTO> Items, int TotalCount, int RegisteredLastMonthCount, int MiddlemenCount, int TotalUsers)>
        GetUsersPageWithStatsAsync(
            UserListQuery query,
            IReadOnlyCollection<string>? auth0IdFilter = null,
            IReadOnlyCollection<string>? middlemanAuth0Ids = null,
            CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 10 : query.PageSize;

        var filteredQuery = BuildBaseQuery(query, auth0IdFilter);

        var totalCount = await filteredQuery.CountAsync(ct);

        var orderedQuery = ApplyOrdering(filteredQuery, query.OrderBy);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserListItemDTO
            {
                Auth0UserId = u.Auth0UserID,
                Email = u.Email,
                Name = u.ProfileInfo != null ? u.ProfileInfo.Nickname : null,
                RegisteredAt = u.RegistrationDate,
                Roles = new List<string>()
            })
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthAgo = today.AddMonths(-1);

        var middlemanIds = NormalizeAuth0Ids(middlemanAuth0Ids);

        var stats = await dbContext.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalUsers = g.Count(),

                RegisteredLastMonthCount = g.Count(u =>
                    u.RegistrationDate >= monthAgo &&
                    u.RegistrationDate <= today),

                MiddlemenCount = middlemanIds.Length == 0
                    ? 0
                    : g.Count(u => middlemanIds.Contains(u.Auth0UserID))
            })
            .FirstOrDefaultAsync(ct);

        return (
            items,
            totalCount,
            stats?.RegisteredLastMonthCount ?? 0,
            stats?.MiddlemenCount ?? 0,
            stats?.TotalUsers ?? 0);
    }

    private IQueryable<User> BuildBaseQuery(
        UserListQuery query,
        IReadOnlyCollection<string>? auth0IdFilter)
    {
        IQueryable<User> q = dbContext.Users
            .Where(u => !u.IsDeleted)
            .AsNoTracking();

        if (auth0IdFilter is { Count: > 0 })
        {
            var ids = NormalizeAuth0Ids(auth0IdFilter);

            if (ids.Length == 0)
                return q.Where(_ => false);

            q = q.Where(u => ids.Contains(u.Auth0UserID));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var pattern = $"%{query.SearchText.Trim()}%";

            q = q.Where(u =>
                u.ProfileInfo != null &&
                u.ProfileInfo.Nickname != null &&
                EF.Functions.ILike(u.ProfileInfo.Nickname, pattern));
        }

        if (query.RegisteredFrom is not null)
        {
            var from = DateOnly.FromDateTime(query.RegisteredFrom.Value);
            q = q.Where(u => u.RegistrationDate >= from);
        }

        if (query.RegisteredTo is not null)
        {
            var to = DateOnly.FromDateTime(query.RegisteredTo.Value);
            q = q.Where(u => u.RegistrationDate <= to);
        }

        return q;
    }

    private static IQueryable<User> ApplyOrdering(IQueryable<User> q, UserListOrderBy orderBy)
        => orderBy switch
        {
            UserListOrderBy.NicknameAsc =>
                q.OrderBy(u => u.ProfileInfo != null ? u.ProfileInfo.Nickname : null),

            UserListOrderBy.NicknameDesc =>
                q.OrderByDescending(u => u.ProfileInfo != null ? u.ProfileInfo.Nickname : null),

            UserListOrderBy.EmailAsc =>
                q.OrderBy(u => u.Email),

            UserListOrderBy.EmailDesc =>
                q.OrderByDescending(u => u.Email),

            UserListOrderBy.RegisteredAtAsc =>
                q.OrderBy(u => u.RegistrationDate),

            UserListOrderBy.RegisteredAtDesc =>
                q.OrderByDescending(u => u.RegistrationDate),

            _ =>
                q.OrderByDescending(u => u.RegistrationDate),
        };

    private static string[] NormalizeAuth0Ids(IEnumerable<string>? auth0Ids)
        => auth0Ids is null
            ? Array.Empty<string>()
            : auth0Ids
                .Select(TrimAuth0Prefix)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static string TrimAuth0Prefix(string auth0UserId)
        => auth0UserId.StartsWith("auth0|", StringComparison.Ordinal)
            ? auth0UserId["auth0|".Length..]
            : auth0UserId;
}