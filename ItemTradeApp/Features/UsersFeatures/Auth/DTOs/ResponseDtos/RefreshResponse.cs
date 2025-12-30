namespace ItemTradeApp.LoginFeature.Dto.ResponseDtos;

public record RefreshResponse(int Id, string AccessToken, int ExpiresIn, string? RefreshToken = null, string? IdToken = null);