namespace ItemTradeApp.Features.Shared;

public static class EscapePattern
{
    public static string Escape(string input, char escapeChar = '!')
    => input
        .Replace(escapeChar.ToString(), new string(escapeChar, 2))
        .Replace("%", $"{escapeChar}%")
        .Replace("_", $"{escapeChar}_");
    
}