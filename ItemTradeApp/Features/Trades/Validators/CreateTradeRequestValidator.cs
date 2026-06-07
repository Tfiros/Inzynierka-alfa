using FluentValidation;
using ItemTradeApp.Features.Trades.DTOs.Request;

namespace ItemTradeApp.Features.Trades.Validators;

public class CreateTradeRequestValidator : AbstractValidator<CreateTradeRequest>
{
    public CreateTradeRequestValidator()
    {
        RuleFor(x => x.OfferId).GreaterThan(0)
            .WithMessage("Offer id must be greater tahn 0");
        RuleFor(x => x.CustomerId).GreaterThan(0)
            .WithMessage("Customer id must be greater than 0");
        RuleFor(x => x.CounterOfferId).GreaterThan(0).When(x => x.CounterOfferId.HasValue)
            .WithMessage("Counter offer id must be greater than 0");
    }
}