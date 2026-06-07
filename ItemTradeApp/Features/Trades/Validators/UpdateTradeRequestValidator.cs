using FluentValidation;
using ItemTradeApp.Features.Trades.DTOs.Request;

namespace ItemTradeApp.Features.Trades.Validators;

public sealed class UpdateTradeRequestValidator : AbstractValidator<UpdateTradeRequest>
{
    public UpdateTradeRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.HasBuyerItems is not null || x.HasSellerItems is not null)
            .WithMessage("At least one item confirmation value must be provided.");
    }
}