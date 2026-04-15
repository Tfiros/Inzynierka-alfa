using FluentValidation;
using ItemTradeApp.Users.Auth.DTOs.RequestDtos;

namespace ItemTradeApp.Users.Auth.Validators;

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(100);
    }
}