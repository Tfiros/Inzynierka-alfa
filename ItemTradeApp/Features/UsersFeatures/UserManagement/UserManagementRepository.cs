using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Features.UsersFeature.UserManagement;
public interface IUserManagementRepository
{
    Task<User?> GetUserByAuth0IdAsync(string auth0UserId, CancellationToken ct = default);
    Task UpdateUserAsync(User user, CancellationToken ct = default);
    Task<User>  AddUserAsync(User user, CancellationToken ct = default);
    Task        DeleteUserByAuth0IdAsync(string auth0UserId, CancellationToken ct = default);
}
public class UserManagementRepository (AppDbContext dbContext) : IUserManagementRepository
{

    public async Task<User?> GetUserByAuth0IdAsync(string auth0UserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(auth0UserId))
            return null;

        return await dbContext.Users
            .SingleOrDefaultAsync(u => u.Auth0UserID == auth0UserId, ct);
    }

    public async Task UpdateUserAsync(User user, CancellationToken ct = default)
    {
        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync(ct);
    }
    public async Task<User> AddUserAsync(User user, CancellationToken ct = default)
    {
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(ct);
        return user;
    }
    public async Task DeleteUserByAuth0IdAsync(string auth0UserId, CancellationToken ct = default)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(u => u.Auth0UserID == auth0UserId, ct);

        if (user is null)
            return;

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(ct);
    }
}