using FluentValidation;
using ItemTradeApp.Features.ItemsManagement.Games.DTOs;

namespace ItemTradeApp.Features.ItemsManagement.Games.Validators;

public class UpdateGameRequestValidator : AbstractValidator<UpdateGameRequest>
{
    public UpdateGameRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.GenreId).GreaterThan(0);
    }
}