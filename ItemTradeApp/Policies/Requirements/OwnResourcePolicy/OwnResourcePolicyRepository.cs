using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace ItemTradeApp.Policies.OwnResourcePolicy;

public interface IOwnResourcePolicyRepository
{
    Task<User?> GetUserByAuthZeroId(string authZeroId);
}
public class OwnResourcePolicyRepository(AppDbContext db) : IOwnResourcePolicyRepository
{
    public async Task<User?> GetUserByAuthZeroId(string authZeroId)
    {
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Auth0UserID == authZeroId);
    }
}