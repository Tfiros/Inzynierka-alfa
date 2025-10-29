namespace ItemTradeApp.LoginFeature;

public record RefreshTokenRequest(string RefreshToken, string? Scope = null);