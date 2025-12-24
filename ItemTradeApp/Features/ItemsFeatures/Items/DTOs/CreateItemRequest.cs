namespace ItemTradeApp.Features.ItemsFeatures.Items.DTOs;

public sealed record CreateItemRequest(string Name, int EstimatedTokenValue, int GameId, int ItemRarityId);
