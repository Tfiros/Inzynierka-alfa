namespace ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;

public record ItemDTO(int id, string Name, string Photo_URL, int EstimatedTokenValue, int GameId, string GameName);