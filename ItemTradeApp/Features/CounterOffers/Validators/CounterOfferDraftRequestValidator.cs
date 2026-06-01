using FluentValidation;
using ItemTradeApp.Features.CounterOffers.DTOs.RequestDTOs;
using ItemTradeApp.Features.Offers.Validators;

namespace ItemTradeApp.Features.CounterOffers.Validators;

public sealed class CounterOfferDraftRequestValidator : AbstractValidator<CounterOfferDraftRequest>
{
    public CounterOfferDraftRequestValidator()
    {
        RuleFor(x => x.TokensOffered)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Items)
            .NotNull();

        RuleFor(x => x.Items)
            .Must(items => items is not null && items.Any());

        RuleForEach(x => x.Items)
            .SetValidator(new OfferItemDTOValidator());
    }
}