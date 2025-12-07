namespace ItemTradeApp.Features.UsersFeature.UserInfo;

public static class UserLevelCalculator
{
    // todo change to db procedure
    public static int CalculateLevel(int experience)
    {
        var level = (experience / 1000) + 1;
        return level < 1 ? 1 : level;
    }
}