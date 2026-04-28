using FluentValidation;
using ItemTradeApp.Features.Shared.Notifications.Contracts;

namespace ItemTradeApp.Features.Shared.Notifications.Validators;

public class CreateNotificationRequestValidator : AbstractValidator<CreateNotificationRequest>
{
    public CreateNotificationRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(200);
    }
}