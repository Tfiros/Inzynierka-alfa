using FluentValidation;
using ItemTradeApp.Features.EmailsNotifications.Notifications.Contracts;

namespace ItemTradeApp.Features.EmailsNotifications.Notifications.Validators;

public class MarkReadManyRequestValidator : AbstractValidator<MarkReadManyRequest>
{
    public MarkReadManyRequestValidator()
    {
        RuleFor(x => x.Ids).NotEmpty();
        RuleForEach(x => x.Ids).GreaterThan(0);
    }
}