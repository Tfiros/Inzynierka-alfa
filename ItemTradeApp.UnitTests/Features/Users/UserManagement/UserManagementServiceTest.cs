using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Shared.TokenEscrow;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration.DTOs.Response;
using ItemTradeApp.Features.Users.UserManagement;
using ItemTradeApp.Features.Users.UserManagement.DTOs.Internal;
using ItemTradeApp.Features.Users.UserManagement.DTOs.Request;
using ItemTradeApp.Features.Users.UserManagement.DTOs.Response;
using ItemTradeApp.Persistence;
using ItemTradeApp.Persistence.Models;
using ItemTradeApp.Features.Shared;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Moq;

namespace ItemTradeApp.UnitTests.Features.Users.UserManagement;

public sealed class UserManagementServiceTests
{
    private readonly Mock<IAuthZeroManagementClient> _auth0 = new();
    private readonly Mock<IUserManagementRepository> _repo = new();
    private readonly Mock<ITokenEscrow> _escrow = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private UserManagementService CreateService()
    {
        var options = Options.Create(new AuthZeroOptions
        {
            Realm = "Username-Password-Authentication"
        });

        return new UserManagementService(
            _auth0.Object,
            _repo.Object,
            _escrow.Object,
            _uow.Object,
            options);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenRequestIsNull_ReturnsBadRequest()
    {
        var service = CreateService();

        var result = await service.UpdateUserAsync(null!);

        Assert.False(result.IsSuccess);
        Assert.Equal("body_required", result.Message);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenAuth0IdIsEmpty_ReturnsBadRequest()
    {
        var service = CreateService();

        var result = await service.UpdateUserAsync(new UpdateUserRequest
        {
            AuthZeroUserId = ""
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("auth0_user_id_required", result.Message);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenUserDoesNotExist_ReturnsNotFound()
    {
        _repo.Setup(x => x.GetUserByAuth0IdAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var service = CreateService();

        var result = await service.UpdateUserAsync(new UpdateUserRequest
        {
            AuthZeroUserId = "auth0|abc"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("user_not_found_local_db", result.Message);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenNoChanges_ReturnsNoContent()
    {
        var user = CreateUser();

        _repo.Setup(x => x.GetUserByAuth0IdAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var service = CreateService();

        var result = await service.UpdateUserAsync(new UpdateUserRequest
        {
            AuthZeroUserId = "auth0|abc",
            Email = user.Email,
            Nickname = null,
            NewPassword = null,
            ProfileDescription = user.ProfileInfo!.Description,
            Roles = null
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("no_changes", result.Message);

        _auth0.Verify(
            x => x.PatchUserAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenEmailAndNicknameChanged_PatchesAuth0_UpdatesLocalUser_AndSaves()
    {
        var user = CreateUser();

        _repo.Setup(x => x.GetUserByAuth0IdAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _auth0.Setup(x => x.PatchUserAsync(
                "auth0|abc",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(new AuthZeroBodyResponse(), "ok"));
        var service = CreateService();

        var result = await service.UpdateUserAsync(new UpdateUserRequest
        {
            AuthZeroUserId = "abc",
            Email = "new@example.com",
            Nickname = "NewNick"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("user_updated", result.Message);
        Assert.Equal("new@example.com", user.Email);
        Assert.Equal("NewNick", user.ProfileInfo!.Nickname);

        _auth0.Verify(x => x.PatchUserAsync(
            "auth0|abc",
            It.Is<Dictionary<string, object>>(p =>
                (string)p["email"] == "new@example.com" &&
                (bool)p["verify_email"] == true &&
                (bool)p["email_verified"] == false &&
                (string)p["nickname"] == "NewNick" &&
                (string)p["name"] == "NewNick"),
            It.IsAny<CancellationToken>()), Times.Once);

        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteUserAsync_WhenAuth0IdIsEmpty_ReturnsBadRequest()
    {
        var service = CreateService();

        var result = await service.DeleteUserAsync("");

        Assert.False(result.IsSuccess);
        Assert.Equal("auth0_user_id_required", result.Message);
    }

    [Fact]
    public async Task DeleteUserAsync_WhenUserDoesNotExist_ReturnsNotFound()
    {
        _repo.Setup(x => x.GetUserByAuth0IdAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var service = CreateService();

        var result = await service.DeleteUserAsync("auth0|abc");

        Assert.False(result.IsSuccess);
        Assert.Equal("user_not_found_local_db", result.Message);
    }

    [Fact]
    public async Task DeleteUserAsync_WhenAuth0DeleteFails_ReturnsFailure_AfterLocalSoftDeleteCommit()
    {
        var user = CreateUser();
        var tx = SetupTransaction();

        _repo.Setup(x => x.GetUserByAuth0IdAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        SetupEmptyDeleteUserRefunds(user.ID);

        _repo.Setup(x => x.UpdateOfferStatusesForUserAsync(
                user.ID,
                It.IsAny<IReadOnlyCollection<int>>(),
                (int)OfferStatuses.Canceled,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _repo.Setup(x => x.UpdateTradeStatusesForUserAsync(
                user.ID,
                It.IsAny<IReadOnlyCollection<int>>(),
                (int)TradeStatuses.Failed,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _repo.Setup(x => x.UpdateCounterOfferStatusesForUserAsync(
                user.ID,
                (int)CounterOfferStatuses.Denied,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _repo.Setup(x => x.DenyReceivedUserCounterOffersForRefundAsync(
                user.ID,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _auth0.Setup(x => x.DeleteUserAsync(
                "auth0|abc",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.InternalServerError("auth0_admin_delete_user_failed"));

        var service = CreateService();

        var result = await service.DeleteUserAsync("auth0|abc");

        Assert.False(result.IsSuccess);
        Assert.Equal("auth0_admin_delete_user_failed", result.Message);

        _uow.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);

        _repo.Verify(x => x.SoftDeleteUser(user), Times.Once);
    }
    private Mock<IDbContextTransaction> SetupTransaction()
    {
        var tx = new Mock<IDbContextTransaction>();

        tx.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        tx.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        tx.Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _uow.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);

        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        return tx;
    }

    private void SetupEmptyDeleteUserRefunds(int userId)
    {
        _repo.Setup(x => x.GetActiveUserOffersForRefundAsync(
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeleteUserOfferRefund>());

        _repo.Setup(x => x.GetOwnUserCounterOffersForRefundAsync(
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeleteUserCounterOfferRefund>());

        _repo.Setup(x => x.GetReceivedUserCounterOffersForRefundAsync(
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeleteUserCounterOfferRefund>());

        _repo.Setup(x => x.GetTradesInProgressForRefundAsync(
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeleteUserTradeRefund>());
    }
    [Fact]
    public async Task DeleteUserAsync_WhenEverythingIsValid_RefundsTokens_UpdatesStatuses_SoftDeletes_AndCommits()
    {
        var user = CreateUser();
        var tx = SetupTransaction();
        _repo.Setup(x => x.GetUserByAuth0IdAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _auth0.Setup(x => x.DeleteUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(new AuthZeroBodyResponse(), "ok"));

        _repo.Setup(x => x.GetActiveUserOffersForRefundAsync(user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeleteUserOfferRefund>
            {
                new(10)
            });

        _repo.Setup(x => x.GetOwnUserCounterOffersForRefundAsync(user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeleteUserCounterOfferRefund>
            {
                new(user.ID, 20)
            });

        _repo.Setup(x => x.GetReceivedUserCounterOffersForRefundAsync(user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeleteUserCounterOfferRefund>
            {
                new(99, 30)
            });

        _repo.Setup(x => x.GetTradesInProgressForRefundAsync(user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeleteUserTradeRefund>
            {
                new(CustomerId: 1, SellerId: 2, TokensOffered: 40, TokensWanted: 50)
            });

        _escrow.Setup(x => x.TryReleaseOwnEscrowAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _escrow.Setup(x => x.TryRefundEscrowToOtherAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();

        var result = await service.DeleteUserAsync("abc");

        Assert.True(result.IsSuccess);
        Assert.Equal("user_deleted", result.Message);

        _escrow.Verify(x => x.TryReleaseOwnEscrowAsync(user.ID, 10, It.IsAny<CancellationToken>()), Times.Once);
        _escrow.Verify(x => x.TryReleaseOwnEscrowAsync(user.ID, 20, It.IsAny<CancellationToken>()), Times.Once);
        _escrow.Verify(x => x.TryReleaseOwnEscrowAsync(99, 30, It.IsAny<CancellationToken>()), Times.Once);

        _escrow.Verify(x => x.TryRefundEscrowToOtherAsync(1, 2, 40, It.IsAny<CancellationToken>()), Times.Once);
        _escrow.Verify(x => x.TryRefundEscrowToOtherAsync(2, 1, 50, It.IsAny<CancellationToken>()), Times.Once);

        _repo.Verify(x => x.SoftDeleteUser(user), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteUserAsync_WhenOfferEscrowReleaseFails_RollsBack_AndReturnsBadRequest()
    {
        var user = CreateUser();
        var tx = SetupTransaction();
        _repo.Setup(x => x.GetUserByAuth0IdAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _auth0.Setup(x => x.DeleteUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(new AuthZeroBodyResponse(), "ok"));

        _repo.Setup(x => x.GetActiveUserOffersForRefundAsync(user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeleteUserOfferRefund>
            {
                new(10)
            });
        _repo.Setup(x => x.GetOwnUserCounterOffersForRefundAsync(user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeleteUserCounterOfferRefund>());

        _repo.Setup(x => x.GetReceivedUserCounterOffersForRefundAsync(user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeleteUserCounterOfferRefund>());

        _repo.Setup(x => x.GetTradesInProgressForRefundAsync(user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeleteUserTradeRefund>());
        _escrow.Setup(x => x.TryReleaseOwnEscrowAsync(user.ID, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();

        var result = await service.DeleteUserAsync("abc");

        Assert.False(result.IsSuccess);
        Assert.Equal("release_own_offer_escrow_failed", result.Message);

        tx.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        tx.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(x => x.SoftDeleteUser(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task GetUsersAsync_WhenQueryIsNull_ReturnsBadRequest()
    {
        var service = CreateService();

        var result = await service.GetUsersAsync(null!);

        Assert.False(result.IsSuccess);
        Assert.Equal("body_required", result.Message);
    }

    [Fact]
    public async Task GetUsersAsync_WhenValid_ReturnsPagedUsers()
    {
        var users = new List<UserListItemDTO>
        {
            new()
            {
                Auth0UserId = "abc",
                Email = "test@example.com",
                Name = "Tester",
                RegisteredAt = new DateOnly(2025, 1, 1)
            }
        };

        _auth0.Setup(x => x.GetRolesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<AuthZeroRoleResponse>>.Success(new List<AuthZeroRoleResponse>
            {
                new()
                {
                    Id = "role_middleman",
                    Name = "Middleman"
                }
            }));

        _auth0.Setup(x => x.GetUsersInRoleAsync("role_middleman", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<AuthZeroUserSlim>>.Success(new List<AuthZeroUserSlim>
            {
                new()
                {
                    UserId = "auth0|middleman1"
                }
            }));

        _repo.Setup(x => x.GetUsersPageWithStatsAsync(
                It.IsAny<UserListQuery>(),
                It.Is<string[]>(ids => ids.SequenceEqual(new[] { "middleman1" })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((users, 1, 2, 3, 10));

        var service = CreateService();

        var result = await service.GetUsersAsync(new UserListQuery
        {
            Page = 0,
            PageSize = 0
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(10, result.Data.PageSize);
        Assert.Equal(1, result.Data.TotalCount);
        Assert.Equal(1, result.Data.TotalPages);
        Assert.Equal(2, result.Data.RegisteredLastMonthCount);
        Assert.Equal(3, result.Data.MiddlemenCount);
        Assert.Equal(10, result.Data.TotalUsers);
        Assert.Single(result.Data.Elements);
    }

    [Fact]
    public async Task GetUserDetailsAsync_WhenAuth0IdIsEmpty_ReturnsBadRequest()
    {
        var service = CreateService();

        var result = await service.GetUserDetailsAsync("");

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_auth0_user_id", result.Message);
    }

    [Fact]
    public async Task GetUserDetailsAsync_WhenUserDoesNotExist_ReturnsNotFound()
    {
        _repo.Setup(x => x.GetUserByAuth0IdAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var service = CreateService();

        var result = await service.GetUserDetailsAsync("abc");

        Assert.False(result.IsSuccess);
        Assert.Equal("user_not_found", result.Message);
    }

    [Fact]
    public async Task GetUserDetailsAsync_WhenValid_ReturnsDescriptionAndRoles()
    {
        var user = CreateUser();

        _repo.Setup(x => x.GetUserByAuth0IdAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _auth0.Setup(x => x.GetUserRolesAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<AuthZeroRoleResponse>>.Success(new List<AuthZeroRoleResponse>
            {
                new()
                {
                    Id = "role_admin",
                    Name = "Admin"
                },
                new()
                {
                    Id = "role_middleman",
                    Name = "Middleman"
                }
            }));

        var service = CreateService();

        var result = await service.GetUserDetailsAsync("abc");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("Opis profilu", result.Data.ProfileDescription);
        Assert.Contains("Admin", result.Data.Roles);
        Assert.Contains("Middleman", result.Data.Roles);
    }

    private static User CreateUser()
    {
        return new User
        {
            ID = 1,
            Auth0UserID = "abc",
            Email = "old@example.com",
            IsDeleted = false,
            ProfileInfo = new ProfileInfo
            {
                Nickname = "OldNick",
                Description = "Opis profilu"
            }
        };
    }
}