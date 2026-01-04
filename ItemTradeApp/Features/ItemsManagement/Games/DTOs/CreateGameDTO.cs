namespace ItemTradeApp.Features.ItemsManagement.Games.DTOs;

public sealed record CreateGameRequest(string Name, int GenreId, List<string> ItemRaritiesNames);
