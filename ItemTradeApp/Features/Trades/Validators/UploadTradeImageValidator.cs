using FluentValidation;
using ItemTradeApp.Features.Trades.DTOs.Request;

namespace ItemTradeApp.Features.Trades.Validators;

public sealed class UploadTradeImageRequestValidator : AbstractValidator<UploadTradeImageRequest>
{
    private const long MaxFileSizeInBytes = 25 * 1024 * 1024;

    private static readonly string[] Types =
    [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    public UploadTradeImageRequestValidator()
    {
        RuleFor(x => x.Image)
            .NotNull()
            .WithMessage("Image is required.");

        When(x => x.Image is not null, () =>
        {
            RuleFor(x => x.Image!.ContentType)
                .Must(contentType => Types.Contains(contentType))
                .WithMessage("Only JPEG, PNG and WEBP types are allowed.");

            RuleFor(x => x.Image!.Length)
                .LessThanOrEqualTo(MaxFileSizeInBytes)
                .WithMessage("Image size cannot exceed 25 MB.");
        });
    }
}