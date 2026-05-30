using ItemTradeApp.Features.Offers;
using ItemTradeApp.Features.Offers.DTOs.ResponseDTOs;
using ItemTradeApp.Features.Shared.DTOs.ResponseDTOs;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Shared;

public static class OfferQueryExtentions
{
    public static IQueryable<OfferListingDTO> SelectOfferListingDto(this IQueryable<Offer> q)
        => q.Select(o => new
        {
            Offer = o,
            SuccesfulTrades =
                o.User.OwningTrades.Count(t => t.TradeStatus_ID == (int)TradeStatuses.SuccesfulRealization),
            CompletedTrades = o.User.OwningTrades.Count(t =>
                t.TradeStatus_ID == (int)TradeStatuses.SuccesfulRealization ||
                t.TradeStatus_ID == (int)TradeStatuses.Failed),
            Rating = o.User.Rates.Select(r => (decimal?)r.Mark).Average() ?? 0m
        }).Select(o => new OfferListingDTO
        (new OfferCoreDTO(o.Offer.ID, o.Offer.Title, o.Offer.Description, o.Offer.ExpDate, o.Offer.CreationDate, o.Offer.TokenCost, o.Offer.OfferStatus.ID, o.Offer.IsHighlighted, o.Offer.TokensOffered, o.Offer.TokensWanted),
            new OfferUserDTO
            (
                o.Offer.User_ID,
                o.Offer.User.ProfileInfo!.Nickname,
                o.Offer.User.ProfileInfo!.ImageUrl,
                o.SuccesfulTrades,
                (float)o.Rating,
                o.CompletedTrades == 0 ? 0f : (float)o.SuccesfulTrades / o.CompletedTrades
            ),
            o.Offer.ListingItems.Where(li => !li.IsWanted && !li.Item.IsDeleted).OrderByDescending(li => li.Item.EstimatedTokenValue).Take(OffersConsts.PagedOffersResponseItemAmount)
                .Select(li =>
                    new OfferListingItemDTO
                    (
                        new ItemDTO(li.Item.ID,li.Item.Name,li.Item.Photo_URL,li.Item.EstimatedTokenValue,
                            new GameDTO(li.Item.Game.ID,li.Item.Game.Name,li.Item.Game.Photo_URL,li.Item.Game.Genre_ID)
                        ),
                        li.Quantity,
                        li.Item.Game.Genre.ID,
                        li.Item.Game.Genre.Name,
                        li.Item.ItemRarity.ID,
                        li.Item.ItemRarity.RarityName
                    )).ToList(),
            o.Offer.ListingItems.Where(li => li.IsWanted && !li.Item.IsDeleted).OrderByDescending(li => li.Item.EstimatedTokenValue).Take(OffersConsts.PagedOffersResponseItemAmount)
                .Select(li =>
                    new OfferListingItemDTO
                    (
                        new ItemDTO(li.Item.ID,li.Item.Name,li.Item.Photo_URL,li.Item.EstimatedTokenValue,
                        new GameDTO(li.Item.Game.ID,li.Item.Game.Name,li.Item.Game.Photo_URL,li.Item.Game.Genre_ID)
                        ),
                        li.Quantity,
                        li.Item.Game.Genre.ID,
                        li.Item.Game.Genre.Name,
                        li.Item.ItemRarity.ID,
                        li.Item.ItemRarity.RarityName
                    )).ToList(),
            o.Offer.ListingItems.Count(li => !li.IsWanted && !li.Item.IsDeleted),
            o.Offer.ListingItems.Count(li => li.IsWanted && !li.Item.IsDeleted)
        ));

    public static IQueryable<OfferDetailsDTO> SelectOfferDetailsDto(this IQueryable<Offer> q)
        => q.Select(o => new
        {
            Offer = o,
            SuccesfulTrades =
                o.User.OwningTrades.Count(t => t.TradeStatus_ID == (int)TradeStatuses.SuccesfulRealization),
            CompletedTrades = o.User.OwningTrades.Count(t =>
                t.TradeStatus_ID == (int)TradeStatuses.SuccesfulRealization ||
                t.TradeStatus_ID == (int)TradeStatuses.Failed),
            Rating = o.User.Rates.Select(r => (decimal?)r.Mark).Average() ?? 0m
        }).Select(o => new OfferDetailsDTO(
            new OfferCoreDTO(o.Offer.ID, o.Offer.Title, o.Offer.Description, o.Offer.ExpDate, o.Offer.CreationDate,
                o.Offer.TokenCost, o.Offer.OfferStatus.ID, o.Offer.IsHighlighted, o.Offer.TokensOffered, o.Offer.TokensWanted),
            new OfferUserDTO(o.Offer.User.ID, o.Offer.User.ProfileInfo!.Nickname, o.Offer.User.ProfileInfo.ImageUrl,
                o.SuccesfulTrades, (float)o.Rating,
                o.CompletedTrades == 0 ? 0f : (float)o.SuccesfulTrades / o.CompletedTrades),
            o.Offer.ListingItems.Where(li => !li.IsWanted && !li.Item.IsDeleted).Select(li =>      new OfferListingItemDTO
            (
                new ItemDTO(li.Item.ID,li.Item.Name,li.Item.Photo_URL,li.Item.EstimatedTokenValue,
                    new GameDTO(li.Item.Game.ID,li.Item.Game.Name,li.Item.Game.Photo_URL,li.Item.Game.Genre_ID)
                ),
                li.Quantity,
                li.Item.Game.Genre.ID,
                li.Item.Game.Genre.Name,
                li.Item.ItemRarity.ID,
                li.Item.ItemRarity.RarityName
            )).ToList(),
            o.Offer.ListingItems.Where(li => li.IsWanted && !li.Item.IsDeleted).Select(li =>      new OfferListingItemDTO
            (
                new ItemDTO(li.Item.ID,li.Item.Name,li.Item.Photo_URL,li.Item.EstimatedTokenValue,
                    new GameDTO(li.Item.Game.ID,li.Item.Game.Name,li.Item.Game.Photo_URL,li.Item.Game.Genre_ID)
                ),
                li.Quantity,
                li.Item.Game.Genre.ID,
                li.Item.Game.Genre.Name,
                li.Item.ItemRarity.ID,
                li.Item.ItemRarity.RarityName
            )).ToList()
        ));
}