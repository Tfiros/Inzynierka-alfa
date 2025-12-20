namespace ItemTradeApp.Features.ItemsFeatures.Shared;

public record DropdownDTO(int id, string Name);
public record DropdownResponse(List<DropdownDTO> Items);