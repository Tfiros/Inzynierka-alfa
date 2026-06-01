using FluentValidation;
using ItemTradeApp.Features.Trades.DTOs.Request;

namespace ItemTradeApp.Features.Trades.Validators;

public sealed class UploadTradeImageRequestValidator : AbstractValidator<UploadTradeImageRequest>
{
    private static readonly string[] Types =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    public UploadTradeImageRequestValidator()
    {
        RuleFor(x => x.Image)
            .NotNull();

        When(x => x.Image is not null, () =>
        {
            RuleFor(x => x.Image.ContentType)
                .Must(contentType => Types.Contains(contentType));
        });
    }
}