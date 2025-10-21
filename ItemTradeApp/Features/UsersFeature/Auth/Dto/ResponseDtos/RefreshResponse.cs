namespace ItemTradeApp.LoginFeature.Dto.ResponseDtos;

public record RefreshResponse(string AccessToken, int ExpiresIn, string? RefreshToken = null, string? IdToken = null);