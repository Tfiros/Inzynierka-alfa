namespace ItemTradeApp.LoginFeature.Dto.ResponseDtos;

public record LoginResponseDto(string AccessToken, int ExpiresIn, string? RefreshToken = null, string? IdToken = null);