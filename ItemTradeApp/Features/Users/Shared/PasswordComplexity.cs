namespace ItemTradeApp.Features.Users.Shared;

public static class PasswordComplexity
{
    public static bool SatisfiedComplexity(string password)
    {
        if (string.IsNullOrEmpty(password)) return false;

        var satisfiedCategories = 0;
        if (password.Any(char.IsLower))
        {
            satisfiedCategories++;
        }
        if (password.Any(char.IsUpper))
        {
            satisfiedCategories++;
        }
        if (password.Any(char.IsDigit))
        {
            satisfiedCategories++;
        }
        if (password.Any(c => !char.IsLetterOrDigit(c)))
        {
            satisfiedCategories++;
        }

        return satisfiedCategories >= 3;
    }

}