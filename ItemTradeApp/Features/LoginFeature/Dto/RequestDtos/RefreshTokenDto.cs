namespace ItemTradeApp.LoginFeature.Dto.RequestDtos;

public record RefreshTokenDto(string RefreshToken, string? Scope = null);