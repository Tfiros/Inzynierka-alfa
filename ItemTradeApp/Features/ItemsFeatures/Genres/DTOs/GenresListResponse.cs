using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.ItemsFeatures.Genres.DTOs;

public record GenresListResponse(List<GenreDTO> Genres);
