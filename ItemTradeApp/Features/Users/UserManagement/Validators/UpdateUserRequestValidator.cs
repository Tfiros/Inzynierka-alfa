using FluentValidation;
using ItemTradeApp.Features.Users.Shared;
using ItemTradeApp.Features.Users.UserManagement.DTOs.Request;

namespace ItemTradeApp.Features.Users.UserManagement.Validators;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Nickname)
            .MinimumLength(3)
            .MaximumLength(20)
            .When(x => x.Nickname is not null);

        RuleFor(x => x.AuthZeroUserId)
            .MaximumLength(128)
            .When(x => x.AuthZeroUserId is not null);
        
        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(100)
            .When(x => x.Email is not null);
        
        RuleFor(x => x.NewPassword)
            .MinimumLength(8)
            .Must(PasswordComplexity.SatisfiedComplexity)
            .WithMessage("Password must contain characters from at least 3 of: lowercase, uppercase, digit, special character")
            .When(x => x.NewPassword is not null);

        RuleForEach(x => x.Roles).NotEmpty().When(x => x.Roles is not null);
    }
}