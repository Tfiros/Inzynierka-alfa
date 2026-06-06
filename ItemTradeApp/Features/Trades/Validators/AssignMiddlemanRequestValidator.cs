using FluentValidation;
using ItemTradeApp.Features.Trades.DTOs.Request;

namespace ItemTradeApp.Features.Trades.Validators;

public class AssignMiddlemanRequestValidator : AbstractValidator<AssignMiddlemanRequest>
{
    public AssignMiddlemanRequestValidator()
    {
        RuleFor(x => x.TradeId).GreaterThan(0)
            .WithMessage("Trade id must be greater than 0");
        RuleFor(x => x.TradeId).NotNull()
            .WithMessage("Trade id can't be null");
    }
}