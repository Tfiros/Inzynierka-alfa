using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Offers.Internal;

internal sealed record DictItemQuantity(
    Item Item, int Quantity);