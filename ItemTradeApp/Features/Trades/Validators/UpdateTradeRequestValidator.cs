using FluentValidation;
using ItemTradeApp.Features.Trades.DTOs.Request;

namespace ItemTradeApp.Features.Trades.Validators;

public sealed class UpdateTradeRequestValidator : AbstractValidator<UpdateTradeRequest>
{
    public UpdateTradeRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.HasBuyerItems is not null or false || x.HasSellerItems is not null or false)
            .WithMessage("MiddleMan can't have items");
    }
}