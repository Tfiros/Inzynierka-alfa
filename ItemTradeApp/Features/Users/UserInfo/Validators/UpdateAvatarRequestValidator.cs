using FluentValidation;
using ItemTradeApp.Features.Users.UserInfo.DTOs.Request;

namespace ItemTradeApp.Features.Users.UserInfo.Validators;

public class UpdateAvatarRequestValidator : AbstractValidator<UpdateAvatarRequest>
{
    private const long MaxFileSizeByte = 5 * 1024 * 1024;
    private static readonly string[] AllowedContentTypes = ["image/png", "image/jpeg", "image/webp"];
    public UpdateAvatarRequestValidator()
    {
        RuleFor(x => x.Image)
            .NotNull().WithMessage("Image is required.");
        
        RuleFor(x => x.Image)
            .Must(f => f.Length is > 0 and <= MaxFileSizeByte)
                .WithMessage("Image must have size smaller than 5 MB.")
            .Must(f => AllowedContentTypes.Contains(f.ContentType))
                .WithMessage("Image must be PNG/JPEG/WEBP.")
            .When(x => x.Image is not null);

    }
}