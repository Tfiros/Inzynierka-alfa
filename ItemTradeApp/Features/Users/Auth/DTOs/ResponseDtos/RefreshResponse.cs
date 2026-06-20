namespace ItemTradeApp.Features.Users.Auth.DTOs.ResponseDtos;

public record RefreshResponse(int Id, int ExpiresIn, string? AccessToken = null, string? RefreshToken = null );