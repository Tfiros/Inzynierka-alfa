using FluentValidation;
using ItemTradeApp.Features.Users.UserSettings.DTOs;

namespace ItemTradeApp.Features.Users.UserSettings.Validators;

public class UserDataUpdateRequestValidator : AbstractValidator<UserDataUpdateRequest>
{
    public UserDataUpdateRequestValidator()
    {
        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(100)
            .When(x => x.Email is not null);
        RuleFor(x => x.DateOfBirth)
            .LessThan(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth must be in the past.")
            .When(x => x.DateOfBirth is not null);
    }
}