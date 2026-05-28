using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration.DTOs.Response;
using ItemTradeApp.Features.Users.UserSettings;
using ItemTradeApp.Features.Users.UserSettings.DTOs;
using ItemTradeApp.Persistence.Models;
using Moq;

namespace ItemTradeApp.UnitTests.Features.Users.UserSettings;

public sealed class UserSettingsServiceTests
{
    private readonly Mock<IUserSettingsRepository> _repo = new();
    private readonly Mock<IAuthZeroManagementClient> _auth0 = new();

    private UserSettingsService CreateService()
        => new(_repo.Object, _auth0.Object);

    [Fact]
    public async Task UpdateSensitiveDataAsync_WhenRequestIsNull_ReturnsBadRequest()
    {
        var service = CreateService();

        var result = await service.UpdateSensitiveDataAsync("auth0|abc", null!);

        Assert.False(result.IsSuccess);
        Assert.Equal("Body is required.", result.Message);
    }

    [Fact]
    public async Task UpdateSensitiveDataAsync_WhenAuth0UserIdIsEmpty_ReturnsUnauthorized()
    {
        var service = CreateService();

        var result = await service.UpdateSensitiveDataAsync("", new UserDataUpdateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing auth0 user id (sub claim).", result.Message);
    }

    [Fact]
    public async Task UpdateSensitiveDataAsync_WhenUserDoesNotExist_ReturnsNotFound()
    {
        _repo.Setup(x => x.GetUserByAuth0IdAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var service = CreateService();

        var result = await service.UpdateSensitiveDataAsync(
            "auth0|abc",
            new UserDataUpdateRequest
            {
                Email = "new@example.com"
            });

        Assert.False(result.IsSuccess);
        Assert.Equal("user_not_found", result.Message);

        _auth0.Verify(x => x.PatchUserAsync(
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _repo.Verify(x => x.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSensitiveDataAsync_WhenNoChanges_ReturnsNoContent()
    {
        var user = CreateUser();

        _repo.Setup(x => x.GetUserByAuth0IdAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var service = CreateService();

        var result = await service.UpdateSensitiveDataAsync(
            "auth0|abc",
            new UserDataUpdateRequest
            {
                Email = "old@example.com",
                DateOfBirth = new DateOnly(2000, 1, 1)
            });

        Assert.True(result.IsSuccess);
        Assert.Equal("no_changes", result.Message);

        _auth0.Verify(x => x.PatchUserAsync(
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _repo.Verify(x => x.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateSensitiveDataAsync_WhenOnlyDateOfBirthChanged_UpdatesLocalUserOnly()
    {
        var user = CreateUser();

        _repo.Setup(x => x.GetUserByAuth0IdAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var service = CreateService();

        var result = await service.UpdateSensitiveDataAsync(
            "auth0|abc",
            new UserDataUpdateRequest
            {
                DateOfBirth = new DateOnly(2001, 2, 3)
            });

        Assert.True(result.IsSuccess);
        Assert.Equal("user_sensitive_data_updated", result.Data);
        Assert.Equal(new DateOnly(2001, 2, 3), user.DateOfBirth);
        Assert.Equal("old@example.com", user.Email);

        _auth0.Verify(x => x.PatchUserAsync(
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _repo.Verify(x => x.UpdateUserAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSensitiveDataAsync_WhenOnlyEmailChanged_PatchesAuth0_UpdatesLocalUser()
    {
        var user = CreateUser();

        _repo.Setup(x => x.GetUserByAuth0IdAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _auth0.Setup(x => x.PatchUserAsync(
                "auth0|abc",
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(new AuthZeroBodyResponse(), "ok"));

        var service = CreateService();

        var result = await service.UpdateSensitiveDataAsync(
            "auth0|abc",
            new UserDataUpdateRequest
            {
                Email = "new@example.com"
            });

        Assert.True(result.IsSuccess);
        Assert.Equal("user_sensitive_data_updated", result.Data);
        Assert.Equal("new@example.com", user.Email);
        Assert.Equal(new DateOnly(2000, 1, 1), user.DateOfBirth);

        _auth0.Verify(x => x.PatchUserAsync(
            "auth0|abc",
            It.Is<object>(payload =>
                HasPropertyValue(payload, "email", "new@example.com") &&
                HasPropertyValue(payload, "verify_email", true) &&
                HasPropertyValue(payload, "email_verified", false)),
            It.IsAny<CancellationToken>()), Times.Once);

        _repo.Verify(x => x.UpdateUserAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSensitiveDataAsync_WhenEmailAndDateOfBirthChanged_PatchesAuth0_AndUpdatesLocalUser()
    {
        var user = CreateUser();

        _repo.Setup(x => x.GetUserByAuth0IdAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _auth0.Setup(x => x.PatchUserAsync(
                "auth0|abc",
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(new AuthZeroBodyResponse(), "ok"));

        var service = CreateService();

        var result = await service.UpdateSensitiveDataAsync(
            "auth0|abc",
            new UserDataUpdateRequest
            {
                Email = "new@example.com",
                DateOfBirth = new DateOnly(2002, 5, 10)
            });

        Assert.True(result.IsSuccess);
        Assert.Equal("user_sensitive_data_updated", result.Data);
        Assert.Equal("new@example.com", user.Email);
        Assert.Equal(new DateOnly(2002, 5, 10), user.DateOfBirth);

        _auth0.Verify(x => x.PatchUserAsync(
            "auth0|abc",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _repo.Verify(x => x.UpdateUserAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateSensitiveDataAsync_WhenAuth0PatchFails_ReturnsFailure_AndDoesNotUpdateLocalUser()
    {
        var user = CreateUser();

        _repo.Setup(x => x.GetUserByAuth0IdAsync("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _auth0.Setup(x => x.PatchUserAsync(
                "auth0|abc",
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.BadRequest("auth0_update_user_failed"));

        var service = CreateService();

        var result = await service.UpdateSensitiveDataAsync(
            "auth0|abc",
            new UserDataUpdateRequest
            {
                Email = "new@example.com"
            });

        Assert.False(result.IsSuccess);
        Assert.Equal("auth0_update_user_failed", result.Message);
        Assert.Equal("old@example.com", user.Email);

        _repo.Verify(x => x.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetSecurityProfileInfoAsync_WhenUserDoesNotExist_ReturnsNotFound()
    {
        _repo.Setup(x => x.GetUserWithProfileInfoByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var service = CreateService();

        var result = await service.GetSecurityProfileInfoAsync(1);

        Assert.False(result.IsSuccess);
        Assert.Equal("user_or_profile_info_not_found: User or profile info not found", result.Message);
    }

    [Fact]
    public async Task GetSecurityProfileInfoAsync_WhenProfileInfoDoesNotExist_ReturnsNotFound()
    {
        var user = CreateUser();
        user.ProfileInfo = null;

        _repo.Setup(x => x.GetUserWithProfileInfoByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var service = CreateService();

        var result = await service.GetSecurityProfileInfoAsync(1);

        Assert.False(result.IsSuccess);
        Assert.Equal("user_or_profile_info_not_found: User or profile info not found", result.Message);
    }

    [Fact]
    public async Task GetSecurityProfileInfoAsync_WhenUserExists_ReturnsSecurityInfo()
    {
        var user = CreateUser();

        _repo.Setup(x => x.GetUserWithProfileInfoByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var service = CreateService();

        var result = await service.GetSecurityProfileInfoAsync(1);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Id);
        Assert.Equal(new DateOnly(2000, 1, 1), result.Data.DateOfBirth);
        Assert.Equal("old@example.com", result.Data.Email);
    }

    private static User CreateUser()
    {
        return new User
        {
            ID = 1,
            Auth0UserID = "abc",
            Email = "old@example.com",
            DateOfBirth = new DateOnly(2000, 1, 1),
            IsDeleted = false,
            ProfileInfo = new ProfileInfo
            {
                Nickname = "Tester",
                Description = "Opis"
            }
        };
    }

    private static bool HasPropertyValue<TValue>(object obj, string propertyName, TValue expectedValue)
    {
        var property = obj.GetType().GetProperty(propertyName);
        if (property is null)
            return false;

        var value = property.GetValue(obj);

        return EqualityComparer<TValue>.Default.Equals((TValue?)value, expectedValue);
    }
}