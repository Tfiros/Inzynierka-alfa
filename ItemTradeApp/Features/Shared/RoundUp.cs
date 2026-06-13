namespace ItemTradeApp.Features.Shared;

public class RoundUp
{
    public static float RoundToTwo(float value)
    {
        return (float)Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
