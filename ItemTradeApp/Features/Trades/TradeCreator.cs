using ItemTradeApp.Features.Shared.TradeCreation;
using ItemTradeApp.Features.Shared.TradeCreation.DTOs;
using ItemTradeApp.Features.Trades.Repositories;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;

namespace ItemTradeApp.Features.Trades;

public sealed class TradeCreator(ITradeRepository tradeRepository, IOfferRepository offerRepository, ICounterOfferRepository counterOfferRepository) : ITradeCreation
{
    public async Task<Trade> ExecuteAsync(CreateTradeContext context, CancellationToken ct)
    {

        if (context.BuyerId == context.SellerId)
        {
            throw new TradeGuardViolationException("trade_buyer_equals_seller");
        }

        var offerSides = await offerRepository.GetOfferSidesItems(context.OfferId, ct);
        if (offerSides is null)
        {
            throw new TradeGuardViolationException("trade_offer_not_found");
        }
        
        bool buyerGivesItems;
        bool buyerGivesTokens;
        if (context.CounterOfferId is null)
        {
            buyerGivesItems = offerSides.HasWantedItems;
            buyerGivesTokens = offerSides.TokensWanted > 0;
        }
        else
        {
            var counterOfferSide =
                await counterOfferRepository.CounterOfferHasItemsAsync(context.CounterOfferId.Value, context.OfferId,
                    ct);
            if (counterOfferSide is null)
            {
                throw new TradeGuardViolationException("trade_counter_offer_not_found");    
            }

            buyerGivesItems = counterOfferSide.HasItems;
            buyerGivesTokens = counterOfferSide.TokensOffered > 0;

        }

        if (!offerSides.HasOfferedItems && offerSides.TokensOffered <= 0)
        {
            throw new TradeGuardViolationException("trade_seller_side_gives_nothing");
        }
        if (!buyerGivesItems && !buyerGivesTokens)
        {
            throw new TradeGuardViolationException("trade_buyer_side_gives_nothing");
        }
        
        if ( !offerSides.HasOfferedItems&& !buyerGivesItems )
        {
            throw new TradeGuardViolationException("tokens_for_tokens_forbidden");
        }



        var trade = new Trade
        {
            Offer_ID = context.OfferId,
            AcceptedCounterOffer_ID = context.CounterOfferId,
            Customer_ID = context.BuyerId,
            Seller_ID = context.SellerId,
            CreationDate = DateTime.UtcNow,
            CompletitionDate = null,
            TradeStatus_ID = (int)TradeStatuses.New,
            MiddlemanUser_ID = null,
            HasBuyersItems = false,
            HasSellersItems = false
        };

        await tradeRepository.AddAsync(trade, ct);
        return trade;
    }
}