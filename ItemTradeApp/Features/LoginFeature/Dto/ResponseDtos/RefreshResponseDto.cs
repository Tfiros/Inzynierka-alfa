namespace ItemTradeApp.LoginFeature.Dto.ResponseDtos;

public record RefreshResponseDto(string AccessToken, int ExpiresIn, string? RefreshToken = null, string? IdToken = null);