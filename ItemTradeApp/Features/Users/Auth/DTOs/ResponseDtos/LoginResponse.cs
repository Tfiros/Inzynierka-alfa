namespace ItemTradeApp.Features.Users.Auth.DTOs.ResponseDtos;

public record LoginResponse(int Id, int ExpiresIn,string? AccessToken = null, string? RefreshToken = null);