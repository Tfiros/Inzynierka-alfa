namespace ItemTradeApp.LoginFeature.Dto.ResponseDtos;

public record LoginResponse(int Id, string AccessToken, int ExpiresIn, string? RefreshToken = null, string? IdToken = null);