using FluentValidation;
using ItemTradeApp.Features.ItemsManagement.Items.DTOs;

namespace ItemTradeApp.Features.ItemsManagement.Items.Validators;

public class UpdateItemRequestValidator : AbstractValidator<UpdateItemRequest>
{
    public UpdateItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.EstimatedTokenValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ItemRarityId).GreaterThan(0);
    }
}