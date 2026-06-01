using FluentValidation;
using ItemTradeApp.Features.Trades.DTOs.Request;

namespace ItemTradeApp.Features.Trades.Validators;

public class AssignMiddlemanRequestValidator : AbstractValidator<AssignMiddlemanRequest>
{
    public AssignMiddlemanRequestValidator()
    {
        RuleFor(x => x.TradeId).GreaterThan(0);
        RuleFor(x => x.TradeId).NotNull();
    }
}