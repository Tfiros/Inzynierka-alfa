using FluentValidation;
using ItemTradeApp.Features.ItemsManagement.Items.DTOs;

namespace ItemTradeApp.Features.ItemsManagement.Items.Validators;

public class CreateItemRequestValidator : AbstractValidator<CreateItemRequest>
{
    public CreateItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.EstimatedTokenValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GameId).GreaterThan(0);
        RuleFor(x => x.ItemRarityId).GreaterThan(0);

    }
}