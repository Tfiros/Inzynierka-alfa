using FluentValidation;
using ItemTradeApp.Features.ItemsManagement.ItemRarities.DTOs;

namespace ItemTradeApp.Features.ItemsManagement.ItemRarities.Validators;

public class UpdateItemRarityRequestValidator : AbstractValidator<UpdateItemRarityRequest>
{
    public UpdateItemRarityRequestValidator()
    {
        RuleFor(x => x.RarityName).NotEmpty().MaximumLength(20);
    }
}