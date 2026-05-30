using FluentValidation;
using ItemTradeApp.Features.Users.Auth.DTOs.RequestDtos;

namespace ItemTradeApp.Features.Users.Auth.Validators;

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(100);
    }
}