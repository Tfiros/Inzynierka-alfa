using FluentValidation;
using ItemTradeApp.Features.Trades.DTOs.Request;

namespace ItemTradeApp.Features.Trades.Validators;

public class CompleteAndMarkTradeRequestValidator : AbstractValidator<CompleteAndMarkTradeRequest>
{
    public CompleteAndMarkTradeRequestValidator()
    {
        RuleFor(x => x.BuyersID).GreaterThan(0);
        RuleFor(x => x.BuyersGrade).InclusiveBetween(1, 10);
        RuleFor(x => x.BuyersDescription).NotEmpty().MaximumLength(500);
        RuleFor(x => x.SellersID).GreaterThan(0);
        RuleFor(x => x.SellersGrade).InclusiveBetween(1, 10);
        RuleFor(x => x.SellersDescription).NotEmpty().MaximumLength(500);
    }
}