using FluentValidation;
using ItemTradeApp.Features.Users.UserInfo.DTOs.Request;

namespace ItemTradeApp.Features.Users.UserInfo.Validators;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Nickname).NotEmpty().MinimumLength(3).MaximumLength(20).When(x => x.Nickname is not null);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);

    }
}