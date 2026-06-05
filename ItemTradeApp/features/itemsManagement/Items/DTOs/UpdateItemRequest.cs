namespace ItemTradeApp.Features.ItemsManagement.Items.DTOs;

public sealed record UpdateItemRequest(
    string Name,
    int EstimatedTokenValue,
    int ItemRarityId,
    IFormFile? Image
);