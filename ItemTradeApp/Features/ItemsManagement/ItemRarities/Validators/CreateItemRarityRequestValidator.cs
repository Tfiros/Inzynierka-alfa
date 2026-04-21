using FluentValidation;
using ItemTradeApp.Features.ItemsManagement.ItemRarities.DTOs;

namespace ItemTradeApp.Features.ItemsManagement.ItemRarities.Validators;

public class CreateItemRarityRequestValidator : AbstractValidator<CreateItemRarityRequest>
{
    public CreateItemRarityRequestValidator()
    {
        RuleFor(x => x.GameId).GreaterThan(0);
        RuleFor(x => x.RarityName).NotEmpty().MaximumLength(20);
    }
}