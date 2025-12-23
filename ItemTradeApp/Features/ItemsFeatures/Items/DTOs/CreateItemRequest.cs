namespace ItemTradeApp.Features.ItemsFeatures.Items.DTOs;

public sealed record CreateItemRequest(string Name, int GameId, int estimatedTokenValue);
