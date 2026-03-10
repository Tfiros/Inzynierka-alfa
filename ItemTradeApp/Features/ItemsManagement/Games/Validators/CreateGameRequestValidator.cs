using FluentValidation;
using ItemTradeApp.Features.ItemsManagement.Games.DTOs;

namespace ItemTradeApp.Features.ItemsManagement.Games.Validators;

public class CreateGameRequestValidator : AbstractValidator<CreateGameRequest>
{
    public CreateGameRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.GenreId).GreaterThan(0);
        RuleFor(x => x.ItemRaritiesNames).NotEmpty();
        RuleForEach(x => x.ItemRaritiesNames).NotEmpty().MaximumLength(20);
    }
}