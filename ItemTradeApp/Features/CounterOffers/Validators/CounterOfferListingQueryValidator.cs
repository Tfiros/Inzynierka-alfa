using FluentValidation;
using ItemTradeApp.Features.CounterOffers.DTOs;
using ItemTradeApp.Features.CounterOffers.DTOs.RequestDTOs;
using ItemTradeApp.Features.Users.UserInfo.DTOs.Request;

namespace ItemTradeApp.Features.CounterOffers.Validators;

public sealed class CounterOfferListingsQueryValidator : AbstractValidator<CounterOfferListingsQuery>
{
    public CounterOfferListingsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0)
            .WithMessage("Page must be greater than 0");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100)
            .WithMessage("Page size must be betwee 1 and 100");
        RuleFor(x => x.OrderBy).IsInEnum()
            .WithMessage("Invalid order by");
    }
}