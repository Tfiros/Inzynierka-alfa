using FluentValidation;
using ItemTradeApp.Features.Offers.DTOs.RequestDTOs;

namespace ItemTradeApp.Features.Offers.Validators;

public class OfferUpdateDraftRequestValidator : AbstractValidator<OfferUpdateDraftRequest>
{
    public OfferUpdateDraftRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(120);
        
        RuleFor(x => x.Description)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(2000);

        RuleFor(x => x.DurationDays)
            .Must(d => d is 0 or 7 or 14 or 31)
            .WithMessage("Duration must be 0, 7, 14 or 31.");

        RuleFor(x => x.TokensOffered).GreaterThanOrEqualTo(0);
        
        RuleFor(x => x.TokensWanted).GreaterThanOrEqualTo(0);

        RuleFor(x => x)
            .Must(x => x.OfferedItems.Count > 0 || x.WantedItems.Count > 0)
            .WithMessage("At least one side must contain items.");

        When(x => x.OfferedItems.Count == 0, () =>
        {
            RuleFor(x => x.TokensOffered).GreaterThan(0)
                .WithMessage("Must offer tokens when no items are offered.");
        });

        When(x => x.WantedItems.Count == 0, () =>
        {
            RuleFor(x => x.TokensWanted)
                .GreaterThan(0)
                .WithMessage("Must want tokens when no items are wanted.");
        });

        RuleForEach(x => x.OfferedItems).SetValidator(new OfferItemDTOValidator());
        RuleForEach(x => x.WantedItems).SetValidator(new OfferItemDTOValidator());

    }
}