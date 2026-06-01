using FluentValidation;
using ItemTradeApp.Features.CounterOffers.DTOs;
using ItemTradeApp.Features.CounterOffers.DTOs.RequestDTOs;

namespace ItemTradeApp.Features.CounterOffers.Validators;

public sealed class CounterOfferListingsQueryValidator : AbstractValidator<CounterOfferListingsQuery>
{
    public CounterOfferListingsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.OrderBy).IsInEnum();
    }
}