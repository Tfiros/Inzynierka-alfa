using FluentValidation;
using ItemTradeApp.Features.ContactPage.DTOs;

namespace ItemTradeApp.Features.ContactPage.Validators;

public sealed class ContactDTOValidator : AbstractValidator<ContactDTO>
{
    public ContactDTOValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Subject)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Message)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(1000);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(50);
    }
}