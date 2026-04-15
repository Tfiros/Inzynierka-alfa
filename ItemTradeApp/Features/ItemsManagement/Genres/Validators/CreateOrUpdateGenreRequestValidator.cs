using FluentValidation;
using ItemTradeApp.Features.ItemsManagement.Genres.DTOs;

namespace ItemTradeApp.Features.ItemsManagement.Genres.Validators;

public class CreateOrUpdateGenreRequestValidator : AbstractValidator<CreateOrUpdateGenreRequest>
{
    public CreateOrUpdateGenreRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(20);
    }
}