namespace ItemTradeApp.Features.ItemsManagement.Items.DTOs;

public sealed record CreateItemRequest(string Name, int EstimatedTokenValue, int GameId, int ItemRarityId);
