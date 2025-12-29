namespace ItemTradeApp.Features.ItemsFeatures.Games.DTOs;

public sealed record CreateGameRequest(string Name, int GenreId, List<string> ItemRaritiesNames);
