namespace ItemTradeApp.Features.ItemsFeatures.Items.DTOs;

public sealed record UpdateItemRequest(string Name, int estimatedTokenValue, int RarityItemId);
