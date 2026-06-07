using System.Security.Claims;
using ItemTradeApp.Features.Shared.Notifications.DTOs;
using ItemTradeApp.Features.Shared.Notifications.Repositories;
using ItemTradeApp.Features.Shared.Notifications.Services;
using ItemTradeApp.Persistence.Models;
using Moq;

namespace ItemTradeApp.UnitTests.Features.Shared.Notifications.Services;

public sealed class NotificationsServiceTests
{
    private readonly Mock<INotificationsRepository> _repo = new();
    private readonly Mock<IUserIdentityRepository> _identityRepo = new();

    private NotificationsService CreateService()
        => new(_repo.Object, _identityRepo.Object);

    private static ClaimsPrincipal User(string auth0Id = "auth0|123")
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, auth0Id)
        }, "TestAuth"));
    }

    private void SetupUserMapping(int? userId = 10)
    {
        _identityRepo
            .Setup(x => x.GetUserIdByAuth0IdAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
    }
    [Fact]
    public async Task GetNotificationsAsync_WhenUserMappingDoesNotExist_ReturnsUnauthorized()
    {
        SetupUserMapping(null);

        var service = CreateService();

        var result = await service.GetNotificationsAsync(
            User(),
            20,
            null,
            null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("No userId mapping.", result.Message);

        _repo.Verify(x => x.GetForUserCursorAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetNotificationsAsync_WhenNotificationsExist_ReturnsMappedDtos()
    {
        SetupUserMapping();

        var now = DateTimeOffset.UtcNow;

        var notifications = new List<Notification>
        {
            new()
            {
                Id = 1,
                UserId = 10,
                Title = "First",
                Message = "Message 1",
                CreatedAt = now,
                ReadAt = null
            },
            new()
            {
                Id = 2,
                UserId = 10,
                Title = "Second",
                Message = "Message 2",
                CreatedAt = now.AddMinutes(-1),
                ReadAt = now
            }
        };

        _repo
            .Setup(x => x.GetForUserCursorAsync(
                10,
                21,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        var service = CreateService();

        var result = await service.GetNotificationsAsync(
            User(),
            20,
            null,
            null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.False(result.Data.HasMore);
        Assert.Null(result.Data.NextCursorCreatedAt);
        Assert.Null(result.Data.NextCursorId);

        Assert.Equal(1, result.Data.Items[0].Id);
        Assert.Equal("First", result.Data.Items[0].Title);
        Assert.False(result.Data.Items[0].IsRead);

        Assert.Equal(2, result.Data.Items[1].Id);
        Assert.True(result.Data.Items[1].IsRead);
    }

    [Fact]
    public async Task GetNotificationsAsync_WhenMoreItemsThanTake_ReturnsCursorAndHasMore()
    {
        SetupUserMapping();

        var now = DateTimeOffset.UtcNow;

        var notifications = new List<Notification>
        {
            new() { Id = 1, UserId = 10, Title = "T1", Message = "M1", CreatedAt = now },
            new() { Id = 2, UserId = 10, Title = "T2", Message = "M2", CreatedAt = now.AddMinutes(-1) },
            new() { Id = 3, UserId = 10, Title = "T3", Message = "M3", CreatedAt = now.AddMinutes(-2) }
        };

        _repo
            .Setup(x => x.GetForUserCursorAsync(
                10,
                3,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        var service = CreateService();

        var result = await service.GetNotificationsAsync(
            User(),
            2,
            null,
            null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.HasMore);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.Equal(2, result.Data.NextCursorId);
        Assert.Equal(notifications[1].CreatedAt, result.Data.NextCursorCreatedAt);
    }

    [Fact]
    public async Task GetNotificationsAsync_WhenTakeIsLessOrEqualZero_UsesDefaultTake()
    {
        SetupUserMapping();

        _repo
            .Setup(x => x.GetForUserCursorAsync(
                10,
                21,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = CreateService();

        var result = await service.GetNotificationsAsync(
            User(),
            0,
            null,
            null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        _repo.Verify(x => x.GetForUserCursorAsync(
            10,
            21,
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetNotificationsAsync_WhenTakeIsGreaterThanMax_UsesMaxTake()
    {
        SetupUserMapping();

        _repo
            .Setup(x => x.GetForUserCursorAsync(
                10,
                51,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = CreateService();

        var result = await service.GetNotificationsAsync(
            User(),
            100,
            null,
            null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        _repo.Verify(x => x.GetForUserCursorAsync(
            10,
            51,
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkReadAsync_WhenNotificationIdIsInvalid_ReturnsBadRequest()
    {
        var service = CreateService();

        var result = await service.MarkReadAsync(
            User(),
            0,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid notification id.", result.Message);

        _repo.Verify(x => x.GetByIdAsync(
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkReadAsync_WhenUserMappingDoesNotExist_ReturnsUnauthorized()
    {
        SetupUserMapping(null);

        var service = CreateService();

        var result = await service.MarkReadAsync(
            User(),
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("No userId mapping.", result.Message);
    }

    [Fact]
    public async Task MarkReadAsync_WhenNotificationDoesNotExist_ReturnsNotFound()
    {
        SetupUserMapping();

        _repo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        var service = CreateService();

        var result = await service.MarkReadAsync(
            User(),
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Notification not found.", result.Message);
    }

    [Fact]
    public async Task MarkReadAsync_WhenNotificationBelongsToAnotherUser_ReturnsUnauthorized()
    {
        SetupUserMapping(10);

        var notification = new Notification
        {
            Id = 1,
            UserId = 99,
            Title = "Title",
            Message = "Message",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _repo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        var service = CreateService();

        var result = await service.MarkReadAsync(
            User(),
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("You cannot modify someone else's notification.", result.Message);

        _repo.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkReadAsync_WhenNotificationIsUnread_MarksAsReadAndSaves()
    {
        SetupUserMapping(10);

        var notification = new Notification
        {
            Id = 1,
            UserId = 10,
            Title = "Title",
            Message = "Message",
            CreatedAt = DateTimeOffset.UtcNow,
            ReadAt = null
        };

        _repo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        var service = CreateService();

        var result = await service.MarkReadAsync(
            User(),
            1,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Notification marked as read.", result.Message);
        Assert.NotNull(notification.ReadAt);

        _repo.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkReadAsync_WhenNotificationIsAlreadyRead_DoesNotSaveAgain()
    {
        SetupUserMapping(10);

        var notification = new Notification
        {
            Id = 1,
            UserId = 10,
            Title = "Title",
            Message = "Message",
            CreatedAt = DateTimeOffset.UtcNow,
            ReadAt = DateTimeOffset.UtcNow
        };

        _repo
            .Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        var service = CreateService();

        var result = await service.MarkReadAsync(
            User(),
            1,
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        _repo.Verify(x => x.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkReadManyAsync_WhenUserMappingDoesNotExist_ReturnsUnauthorized()
    {
        SetupUserMapping(null);

        var service = CreateService();

        var result = await service.MarkReadManyAsync(
            User(),
            new MarkReadManyRequest([1, 2]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("No userId mapping.", result.Message);
    }

    [Fact]
    public async Task MarkReadManyAsync_WhenIdsAreEmpty_ReturnsBadRequest()
    {
        SetupUserMapping();

        var service = CreateService();

        var result = await service.MarkReadManyAsync(
            User(),
            new MarkReadManyRequest([]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Ids must not be empty.", result.Message);

        _repo.Verify(x => x.MarkReadManyAsync(
            It.IsAny<int>(),
            It.IsAny<List<int>>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkReadManyAsync_FiltersInvalidAndDuplicatedIds()
    {
        SetupUserMapping();

        _repo
            .Setup(x => x.MarkReadManyAsync(
                10,
                It.Is<List<int>>(ids =>
                    ids.Count == 2 &&
                    ids.Contains(1) &&
                    ids.Contains(2)),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var service = CreateService();

        var result = await service.MarkReadManyAsync(
            User(),
            new MarkReadManyRequest([1, 1, 0, -5, 2]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Notifications marked as read.", result.Message);

        _repo.Verify(x => x.MarkReadManyAsync(
            10,
            It.Is<List<int>>(ids =>
                ids.Count == 2 &&
                ids.Contains(1) &&
                ids.Contains(2)),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkReadAllAsync_WhenUserMappingDoesNotExist_ReturnsUnauthorized()
    {
        SetupUserMapping(null);

        var service = CreateService();

        var result = await service.MarkReadAllAsync(
            User(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("No userId mapping.", result.Message);
    }

    [Fact]
    public async Task MarkReadAllAsync_WhenUserExists_MarksAllAsRead()
    {
        SetupUserMapping();

        _repo
            .Setup(x => x.MarkReadAllAsync(
                10,
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var service = CreateService();

        var result = await service.MarkReadAllAsync(
            User(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("All notifications marked as read.", result.Message);

        _repo.Verify(x => x.MarkReadAllAsync(
            10,
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotificationIdIsInvalid_ReturnsBadRequest()
    {
        var service = CreateService();

        var result = await service.DeleteAsync(
            User(),
            0,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid notification id.", result.Message);

        _repo.Verify(x => x.SoftDeleteAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenUserMappingDoesNotExist_ReturnsUnauthorized()
    {
        SetupUserMapping(null);

        var service = CreateService();

        var result = await service.DeleteAsync(
            User(),
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("No userId mapping.", result.Message);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotificationDoesNotExist_ReturnsNotFound()
    {
        SetupUserMapping();

        _repo
            .Setup(x => x.SoftDeleteAsync(
                10,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var service = CreateService();

        var result = await service.DeleteAsync(
            User(),
            1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Notification not found.", result.Message);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotificationExists_ReturnsSuccess()
    {
        SetupUserMapping();

        _repo
            .Setup(x => x.SoftDeleteAsync(
                10,
                1,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = CreateService();

        var result = await service.DeleteAsync(
            User(),
            1,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Notification deleted.", result.Message);
    }
}