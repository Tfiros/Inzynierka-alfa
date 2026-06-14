using ItemTradeApp.Persistence.Models;
using ItemTradeApp.Resources.EmailTemplates.Models;

namespace ItemTradeApp.Features.Shared.Emails.Mappers;

public static class EmailTemplateMapper
{
    public static OfferCreatedEmailModel MapToOfferCreatedEmailModel(Offer offer)
    {
        if (offer is null)
            throw new ArgumentNullException(nameof(offer));

        if (string.IsNullOrWhiteSpace(offer.Title))
            throw new ArgumentException("Offer title is required.", nameof(offer));

        if (offer.ListingItems is null || !offer.ListingItems.Any())
            throw new ArgumentException("Offer must contain at least one listing item.", nameof(offer));

        var invalidListingItem = offer.ListingItems.FirstOrDefault(li =>
            li is null ||
            li.Item is null ||
            string.IsNullOrWhiteSpace(li.Item.Name) ||
            li.Quantity <= 0);

        if (invalidListingItem is not null)
            throw new ArgumentException(
                "Offer contains invalid listing item. Each item must have item data, name and quantity greater than 0.",
                nameof(offer));

        return new OfferCreatedEmailModel
        {
            Name = offer.Title.Trim(),
            CreatedAt = offer.CreationDate,
            ExpiresAt = offer.ExpDate,
            IsFeatured = offer.IsHighlighted,
            TokenAmount = offer.TokensOffered,
            TokensSpent = offer.TokenCost,

            Items = offer.ListingItems
                .Select(li => new EmailItemModel
                {
                    Name = li.Item.Name.Trim(),
                    Amount = li.Quantity
                })
                .ToList()
        };
    } 

    public static TradeFromCounterOfferCreatedEmailModel MapToTradeFromCounterOfferCreatedEmailModel(
        string buyerNick, 
        string sellerNick, 
        Trade trade,
        Offer offer,
        CounterOffer counterOffer) =>
        new TradeFromCounterOfferCreatedEmailModel
        {
            BuyerNickname = buyerNick,
            SellerNickname = sellerNick,
            OfferName = offer.Title,
            CreatedAt = trade.CreationDate,
            SellerItems = offer.ListingItems.Where(li => !li.IsWanted)
                .Select(li => new EmailItemModel
                {
                    Name = li.Item.Name,
                    Amount = li.Quantity
                })
                .ToList(),
            BuyerItems = counterOffer.ListingCounterOfferItems
                .Select(li => new EmailItemModel
                {
                    Name = li.Item.Name,
                    Amount = li.Quantity
                })
                .ToList()
        };

    public static TradeCreatedEmailModel MapToTradeCreatedEmailModel( string buyerNick, 
        string sellerNick, 
        Trade trade,
        Offer offer) =>
        new TradeCreatedEmailModel
        {
            BuyerNickname = buyerNick,
            SellerNickname = sellerNick,
            OfferName = offer.Title,
            CreatedAt = trade.CreationDate,
        };
    public static TradeFinishedEmailModel MapToTradeFinishedEmailModel(string buyerNick, 
        string sellerNick,
        Trade trade,
        Offer offer,
        string? middlemanNick = null) =>  new TradeFinishedEmailModel
    {
        BuyerNickname = buyerNick,
        SellerNickname = sellerNick,
        MiddlemanNickname = middlemanNick ?? string.Empty,
        OfferName = offer.Title,
        CreatedAt = trade.CreationDate,
    };
}