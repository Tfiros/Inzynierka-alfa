namespace ItemTradeApp.LoginFeature;

public record RegisterRequest(string Email, string Password, string? Name = null, Dictionary<string ,object>? Metadata = null);