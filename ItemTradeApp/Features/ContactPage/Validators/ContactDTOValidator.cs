using FluentValidation;
using ItemTradeApp.Features.ContactPage.DTOs;

namespace ItemTradeApp.Features.ContactPage.Validators;

public sealed class ContactDTOValidator : AbstractValidator<ContactDTO>
{
    public ContactDTOValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("Email cant be empty and it must be an correct email");

        RuleFor(x => x.Subject)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Subject cnt and be empty and have it need to have  less or equal 200 letters");

        RuleFor(x => x.Message)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(1000)
            .WithMessage("Message cant be empty. You need to provide bewteen 10 and 1000 lettes");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(50)
            .WithMessage("Name cant be empty and it need to have less or equal 50 letters");
    }
}