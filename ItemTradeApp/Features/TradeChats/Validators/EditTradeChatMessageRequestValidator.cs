using FluentValidation;
using ItemTradeApp.Features.TradeChats.DTOs.Request;


namespace ItemTradeApp.Features.TradeChats.Validators;

public class EditTradeChatMessageRequestValidator : AbstractValidator<EditTradeChatMessageRequest>
{
    EditTradeChatMessageRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message must not be empty")
            .MaximumLength(TradeChatConsts.MessageMaxLength)
            .WithMessage($"Message must shorter than {TradeChatConsts.MessageMaxLength} characters");
    }
}