using FluentValidation;
using ItemTradeApp.Features.Shared.DTOs;


namespace ItemTradeApp.Features.Offers.Validators;

public class OfferItemDTOValidator: AbstractValidator<OfferItemDTO>
{
    public OfferItemDTOValidator()
    {
        RuleFor(x => x.ItemId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}