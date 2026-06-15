namespace ItemTradeApp.Features.Shared;

public class RoundUp
{
    public static float RoundToTwo(float value)
    {
        return (float)Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    public static float CalculateSuccessRate(int successfulTradeCount, int completedTradesCount)
    {
        return completedTradesCount == 0 ? 0f : RoundToTwo((float)successfulTradeCount / completedTradesCount);
    }
}
