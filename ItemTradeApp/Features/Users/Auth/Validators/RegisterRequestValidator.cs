using FluentValidation;
using ItemTradeApp.Features.Users.Shared;
using ItemTradeApp.Features.Users.Shared.DTOs;

namespace ItemTradeApp.Features.Users.Auth.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(100);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Must(PasswordComplexity.SatisfiedComplexity)
            .WithMessage(
                "Password must contain characters from at least 3 of: lowercase, uppercase, digit, special character.");

        RuleFor(x => x.BirthDate)
            .NotEmpty()
            .LessThan(DateTime.UtcNow)
            .WithMessage("Birth date must be in the past.");

        RuleFor(x => x.Username)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(20);
    }
}