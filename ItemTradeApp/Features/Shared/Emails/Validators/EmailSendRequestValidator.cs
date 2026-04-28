using FluentValidation;
using ItemTradeApp.Features.Shared.Emails.Contracts;

namespace ItemTradeApp.Features.Shared.Emails.Validators;

public class EmailSendRequestValidator : AbstractValidator<EmailSendRequest>
{
    public EmailSendRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.HtmlBody).NotEmpty().MaximumLength(10000);
    }
}