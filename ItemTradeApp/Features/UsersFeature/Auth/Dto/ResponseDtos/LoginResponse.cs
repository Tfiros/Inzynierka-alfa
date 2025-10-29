namespace ItemTradeApp.LoginFeature.Dto.ResponseDtos;

public record LoginResponse(string AccessToken, int ExpiresIn, string? RefreshToken = null, string? IdToken = null);