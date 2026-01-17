namespace ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;

public record ItemDTO(int Id, string Name, string Photo_URL, int EstimatedTokenValue, int GameId, string GameName);