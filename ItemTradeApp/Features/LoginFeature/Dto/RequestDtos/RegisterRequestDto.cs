namespace ItemTradeApp.LoginFeature.Dto.RequestDtos;

public record RegisterRequestDto(string Email, string Password, string? Name = null, Dictionary<string, object>? Metadata = null);