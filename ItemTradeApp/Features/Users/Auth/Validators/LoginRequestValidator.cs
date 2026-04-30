using FluentValidation;
using ItemTradeApp.Features.Users.Auth.DTOs.RequestDtos;

namespace ItemTradeApp.Features.Users.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty();
    }
}