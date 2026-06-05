namespace ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;

public record ItemDTO(int Id, string Name, string PhotoUrl, int EstimatedTokenValue, GameDTO Game);