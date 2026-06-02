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

        RuleFor(x => x)
            .Must(x =>
                x.TokensOffered > 0 ||
                (x.Items is not null && x.Items.Any()));
        
        When(x => x.Items is not null && x.Items.Any(), () =>
        {
            RuleForEach(x => x.Items)
                .SetValidator(new OfferItemDTOValidator());
        });
    }
}