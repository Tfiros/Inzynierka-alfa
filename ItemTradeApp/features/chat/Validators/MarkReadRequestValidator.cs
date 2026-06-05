using FluentValidation;
using ItemTradeApp.Features.Chat.DTOs;

namespace ItemTradeApp.Features.Chat.Validators;

public class MarkReadRequestValidator : AbstractValidator<MarkReadRequest>
{
    public MarkReadRequestValidator()
    {
        RuleFor(x => x.LastReadMessageId).GreaterThan(0);
    }
}