namespace ItemTradeApp.Users.Auth.DTOs.ResponseDtos;

public record LoginResponse(int Id, string AccessToken, int ExpiresIn, string? RefreshToken = null, string? IdToken = null);