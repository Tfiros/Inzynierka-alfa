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
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        Console.WriteLine(request.BirthDate);
        try
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
                RegistrationDate = DateOnly.FromDateTime(now)
            };

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            var nickname = !string.IsNullOrWhiteSpace(request.Username)
                ? request.Username!
                : request.Email.Split('@')[0];

            var profile = new ProfileInfo
            {
                User_ID = user.ID,
                Nickname = nickname,
                Description = string.Empty
            };

            profile.User = user;
            user.ProfileInfo = profile;

            dbContext.ProfileInfos.Add(profile);
            await dbContext.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        
    }
}