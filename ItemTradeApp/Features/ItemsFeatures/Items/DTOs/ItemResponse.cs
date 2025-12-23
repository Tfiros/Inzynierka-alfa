namespace ItemTradeApp.Features.ItemsFeatures.Items.DTOs;

public record ItemResponse(int id, string Name, string Photo_URL, int estimatedTokenValue, int gameId, string GameName);