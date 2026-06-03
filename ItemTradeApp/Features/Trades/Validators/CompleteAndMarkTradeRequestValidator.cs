using FluentValidation;
using ItemTradeApp.Features.Trades.DTOs.Request;

namespace ItemTradeApp.Features.Trades.Validators;

public class CompleteAndMarkTradeRequestValidator : AbstractValidator<CompleteAndMarkTradeRequest>
{
    public CompleteAndMarkTradeRequestValidator()
    {
        RuleFor(x => x.BuyersGrade).InclusiveBetween(1, 10)
            .WithMessage("Buyer grade must be between 1 and 10");
        RuleFor(x => x.BuyersDescription).NotEmpty().MaximumLength(500)
            .WithMessage("You need to provide buyer grade description");
        RuleFor(x => x.SellersGrade).InclusiveBetween(1, 10)
            .WithMessage("Seller grade must be between 1 and 10");
        RuleFor(x => x.SellersDescription).NotEmpty().MaximumLength(500)
            .WithMessage("You need to provide seller grade description");
    }
}