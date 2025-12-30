namespace ItemTradeApp.Users.Auth.DTOs.ResponseDtos;

public record RefreshResponse(string AccessToken, int ExpiresIn, string? RefreshToken = null, string? IdToken = null);