using FluentValidation;
using ItemTradeApp.Features.Users.UserManagement.DTOs;
using ItemTradeApp.Features.Users.UserManagement.DTOs.Request;

namespace ItemTradeApp.Features.Users.UserManagement.Validators;

public class UserListQueryValidator: AbstractValidator<UserListQuery>
{
    public UserListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);

        RuleFor(x => x.RegisteredTo)
            .GreaterThanOrEqualTo(x => x.RegisteredFrom)
            .When(x => x.RegisteredFrom is not null && x.RegisteredTo is not null);

        RuleFor(x => x.SearchText).MaximumLength(150).When(x => x.SearchText is not null);

        RuleFor(x => x.OrderBy).IsInEnum();
    }
}