namespace ItemTradeApp.Features.Users.UserInfo;

public static class UserLevelCalculator
{
    public static int CalculateLevel(int experience)
    {
        var level = (experience / 100) + 1;
        return level < 1 ? 1 : level;
    }
}