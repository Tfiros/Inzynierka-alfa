using FluentValidation;
using ItemTradeApp.Features.Offers.DTOs.RequestDTOs;

namespace ItemTradeApp.Features.Offers.Validators;

public class OfferListingsQueryValidator : AbstractValidator<OfferListingsQuery>
{
    public OfferListingsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.GameId).GreaterThan(0).When(x => x.GameId.HasValue);
        RuleFor(x => x.GenreId).GreaterThan(0).When(x => x.GenreId.HasValue);
        RuleFor(x => x.RarityId).GreaterThan(0).When(x => x.RarityId.HasValue);

        RuleFor(x => x.SearchText).MaximumLength(150).When(x => x.SearchText is not null);

        RuleFor(x => x.OrderBy).IsInEnum();

    }
}