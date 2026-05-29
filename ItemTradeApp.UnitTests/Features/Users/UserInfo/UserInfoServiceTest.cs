using ItemTradeApp.Features.Shared.Images;
using ItemTradeApp.Features.Users.UserInfo;
using ItemTradeApp.Features.Users.UserInfo.DTOs.Request;
using ItemTradeApp.Persistence.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;

namespace ItemTradeApp.UnitTests.Features.Users.UserInfo;

public sealed class UserInfoServiceTests
{
    private readonly Mock<IUserInfoRepository> _repo = new();
    private readonly Mock<IImageService> _imageService = new();

    private UserInfoService CreateService()
    {
        var folders = Options.Create(new S3Folders
        {
            Avatars = "avatars"
        });

        return new UserInfoService(_repo.Object, _imageService.Object, folders);
    }

    [Fact]
    public async Task GetNavbarInfoAsync_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        _repo.Setup(x => x.GetUserWithProfileInfoByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var service = CreateService();

        var result = await service.GetNavbarInfoAsync(1);

        Assert.False(result.IsSuccess);
        Assert.Equal("user_or_profile_info_not_found: User not found", result.Message);
    }

    [Fact]
    public async Task GetNavbarInfoAsync_ShouldReturnNavbarInfo_WhenUserExists()
    {
        var user = CreateUser();

        _repo.Setup(x => x.GetUserWithProfileInfoByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _repo.Setup(x => x.GetChatUnreadTotalAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        _repo.Setup(x => x.GetNumberOfUnreadNotifications(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var service = CreateService();

        var result = await service.GetNavbarInfoAsync(1);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Id);
        Assert.Equal("Tester", result.Data.Nickname);
        Assert.Equal("test@example.com", result.Data.Email);
        Assert.Equal(100, result.Data.Tokens);
        Assert.Equal(20, result.Data.EscrowedTokens);
        Assert.Equal(150, result.Data.Experience);
        Assert.Equal(7, result.Data.ChatUnreadTotal);
        Assert.Equal(3, result.Data.NotificationsUnreadTotal);
        Assert.Equal("old-url", result.Data.ImageUrl);
    }

    [Fact]
    public async Task GetProfileInfoAsync_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        _repo.Setup(x => x.GetUserWithProfileInfoByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var service = CreateService();

        var result = await service.GetProfileInfoAsync(1);

        Assert.False(result.IsSuccess);
        Assert.Equal("user_or_profile_info_not_found: User or profile info not found", result.Message);
    }

    [Fact]
    public async Task GetProfileInfoAsync_ShouldReturnNotFound_WhenStatsDoNotExist()
    {
        var user = CreateUser();

        _repo.Setup(x => x.GetUserWithProfileInfoByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _repo.Setup(x => x.GetUserStatsByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((int ActiveOffersCount, int SuccessTradeCount, int CompletedTradeCount, float Rating)?)null);

        var service = CreateService();

        var result = await service.GetProfileInfoAsync(1);

        Assert.False(result.IsSuccess);
        Assert.Equal("user_statistics_not_found", result.Message);
    }

    [Fact]
    public async Task GetProfileInfoAsync_ShouldReturnProfileInfo_WhenUserAndStatsExist()
    {
        var user = CreateUser();

        _repo.Setup(x => x.GetUserWithProfileInfoByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _repo.Setup(x => x.GetUserStatsByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((2, 8, 10, 4.5f));

        var service = CreateService();

        var result = await service.GetProfileInfoAsync(1);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Id);
        Assert.Equal("Tester", result.Data.Nickname);
        Assert.Equal("Opis", result.Data.Description);
        Assert.Equal(2, result.Data.ActiveOffersCount);
        Assert.Equal(8, result.Data.SuccessTradesCount);
        Assert.Equal(4.5f, result.Data.Rating);
        Assert.Equal(0.8f, result.Data.SuccessRate);
    }

    [Fact]
    public async Task UpdateProfileAsync_ShouldTrimAuth0Prefix_AndUpdateProfile()
    {
        var user = CreateUser();

        _repo.Setup(x => x.GetUserWithProfileByAuth0IdAsync("abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _repo.Setup(x => x.GetUserStatsByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1, 5, 10, 4.2f));

        var service = CreateService();

        var result = await service.UpdateProfileAsync(
            "auth0|abc123",
            new UpdateProfileRequest("NowyNick", "Nowy opis"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("NowyNick", user.ProfileInfo!.Nickname);
        Assert.Equal("Nowy opis", user.ProfileInfo.Description);
        Assert.Equal("NowyNick", result.Data!.Nickname);
        Assert.Equal("Nowy opis", result.Data.Description);

        _repo.Verify(x => x.UpdateUserWithProfileInfoAsync(user.ProfileInfo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_ShouldKeepOldValues_WhenRequestValuesAreNull()
    {
        var user = CreateUser();

        _repo.Setup(x => x.GetUserWithProfileByAuth0IdAsync("abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _repo.Setup(x => x.GetUserStatsByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1, 5, 10, 4.2f));

        var service = CreateService();

        var result = await service.UpdateProfileAsync(
            "abc123",
            new UpdateProfileRequest(null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Tester", user.ProfileInfo!.Nickname);
        Assert.Equal("Opis", user.ProfileInfo.Description);
    }

    [Fact]
    public async Task UpdateAvatarAsync_ShouldUploadNewAvatar_UpdateProfile_AndDeleteOldAvatar()
    {
        var user = CreateUser();
        var file = Mock.Of<IFormFile>();

        _repo.Setup(x => x.GetUserWithProfileByAuth0IdAsync("abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _imageService.Setup(x => x.UploadAsync(file, "avatars", It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-url");

        _repo.Setup(x => x.GetUserStatsByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1, 5, 10, 4.2f));

        var service = CreateService();

        var result = await service.UpdateAvatarAsync(
            "auth0|abc123",
            new UpdateAvatarRequest { Image = file },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-url", user.ProfileInfo!.ImageUrl);
        Assert.Equal("new-url", result.Data!.ImageUrl);

        _repo.Verify(x => x.UpdateUserWithProfileInfoAsync(user.ProfileInfo, It.IsAny<CancellationToken>()), Times.Once);
        _imageService.Verify(x => x.DeleteAsync("old-url", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAvatarAsync_ShouldDeleteNewUploadedImage_WhenRepositoryUpdateFails()
    {
        var user = CreateUser();
        var file = Mock.Of<IFormFile>();

        _repo.Setup(x => x.GetUserWithProfileByAuth0IdAsync("abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _imageService.Setup(x => x.UploadAsync(file, "avatars", It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-url");

        _repo.Setup(x => x.UpdateUserWithProfileInfoAsync(user.ProfileInfo!, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("db failed"));

        var service = CreateService();

        var result = await service.UpdateAvatarAsync(
            "auth0|abc123",
            new UpdateAvatarRequest { Image = file },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("avatar_upload_failed", result.Message);

        _imageService.Verify(x => x.DeleteAsync("new-url", It.IsAny<CancellationToken>()), Times.Once);
        _imageService.Verify(x => x.DeleteAsync("old-url", It.IsAny<CancellationToken>()), Times.Never);
    }

    private static User CreateUser()
    {
        return new User
        {
            ID = 1,
            Email = "test@example.com",
            Tokens = 100,
            EscrowedTokens = 20,
            Experience = 150,
            RegistrationDate = new DateOnly(2025, 1, 1),
            ProfileInfo = new ProfileInfo
            {
                Nickname = "Tester",
                Description = "Opis",
                ImageUrl = "old-url"
            },
            Chats = new List<ConversationMember>
            {
                new()
                {
                    ChatConversationId = 10
                },
                new()
                {
                    ChatConversationId = 20
                }
            }
        };
    }
}