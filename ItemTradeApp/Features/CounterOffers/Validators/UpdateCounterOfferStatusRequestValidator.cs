using FluentValidation;
using ItemTradeApp.Features.CounterOffers.DTOs.RequestDTOs;
using ItemTradeApp.Persistence;

namespace ItemTradeApp.Features.CounterOffers.Validators;

public class UpdateCounterOfferStatusRequestValidator 
    : AbstractValidator<UpdateCounterOfferStatusRequest>
{
    public UpdateCounterOfferStatusRequestValidator()
    {
        RuleFor(x => x.StatusId)
            .Must(statusId => Enum.IsDefined(typeof(CounterOfferStatuses), statusId))
            .WithMessage("Invalid counter offer status.");
    }
}