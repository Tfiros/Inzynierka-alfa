namespace ItemTradeApp.Features.Users.Auth.DTOs.RequestDtos;

public record RefreshTokenRequest(string RefreshToken, string? Scope = null);