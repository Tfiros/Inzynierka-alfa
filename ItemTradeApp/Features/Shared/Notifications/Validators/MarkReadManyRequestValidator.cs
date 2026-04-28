using FluentValidation;
using ItemTradeApp.Features.Shared.Notifications.Contracts;

namespace ItemTradeApp.Features.Shared.Notifications.Validators;

public class MarkReadManyRequestValidator : AbstractValidator<MarkReadManyRequest>
{
    public MarkReadManyRequestValidator()
    {
        RuleFor(x => x.Ids).NotEmpty();
        RuleForEach(x => x.Ids).GreaterThan(0);
    }
}