using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.LoginFeature;

public interface IAuthRepository
{
    Task Register(RegisterRequest request, string auth0Id);
}

public class AuthRepository(AppDbContext dbContext) : IAuthRepository
{
    public async Task Register(RegisterRequest request, string auth0Id)
    {
        var now = DateTime.Now;
        
        var user = new User
        {
            Email = request.Email,
            Auth0UserID = auth0Id,
            StripeCustomerID = string.Empty,
            DateOfBirth = DateOnly.FromDateTime(request.BirthDate),
            Tokens = 0,
            Experience = 0,
            TokenExpDate = now,
            RegistrationDate = DateOnly.FromDateTime(now),

            ProfileInfo = new ProfileInfo
            {
                Nickname = request.Username,
                Description = string.Empty
            }
        };

        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync();
    }
}