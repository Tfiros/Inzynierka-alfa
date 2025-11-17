namespace ItemTradeApp.LoginFeature;

public record RegisterRequest(string Email, string Password, DateTime BirthDate, string? Username = null);