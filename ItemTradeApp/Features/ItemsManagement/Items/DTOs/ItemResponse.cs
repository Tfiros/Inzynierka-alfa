namespace ItemTradeApp.Features.ItemsManagement.Items.DTOs;

public record ItemResponse(int id, string Name, string Photo_URL, int EstimatedTokenValue, int GameId, string GameName, int ItemRarityId);
