using FluentValidation;
using ItemTradeApp.Features.CounterOffers.DTOs.RequestDTOs;

namespace ItemTradeApp.Features.CounterOffers.Validators;

public class UpdateCounterOfferStatusRequestValidator : AbstractValidator<UpdateCounterOfferStatusRequest>
{
    public UpdateCounterOfferStatusRequestValidator()
    {
        RuleFor(x => x.StatusId)
            .GreaterThan(0);
        
        RuleFor(x => x.StatusId)
            .LessThanOrEqualTo(4);
    }
}