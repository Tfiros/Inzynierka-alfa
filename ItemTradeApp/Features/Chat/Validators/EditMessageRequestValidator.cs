using FluentValidation;
using ItemTradeApp.Features.Chat.DTOs;

namespace ItemTradeApp.Features.Chat.Validators;

public class EditMessageRequestValidator : AbstractValidator<EditMessageRequest>
{
    public EditMessageRequestValidator ()
    {
        RuleFor(x => x.Message)
            .NotEmpty()
            .MaximumLength(2000)
            .WithMessage("Message must be shorter than 2000 characters.");
    }
}