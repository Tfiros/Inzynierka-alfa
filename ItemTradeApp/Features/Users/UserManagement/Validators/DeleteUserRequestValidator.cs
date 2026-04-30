using FluentValidation;
using ItemTradeApp.Features.Users.UserManagement.DTOs;
using ItemTradeApp.Features.Users.UserManagement.DTOs.Request;

namespace ItemTradeApp.Features.Users.UserManagement.Validators;

public class DeleteUserRequestValidator : AbstractValidator<DeleteUserRequest>
{
    public DeleteUserRequestValidator()
    {
        RuleFor(x => x.AuthZeroUserId).NotEmpty().MaximumLength(128);
    }
}