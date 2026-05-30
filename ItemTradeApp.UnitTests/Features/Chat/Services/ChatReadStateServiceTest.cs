using ItemTradeApp.Features.Chat.Repositories;
using ItemTradeApp.Features.Chat.Services;
using ItemTradeApp.Persistence.Models;
using JetBrains.Annotations;
using Moq;

namespace ItemTradeApp.UnitTests.Features.Chat.Services;

[TestSubject(typeof(ChatReadStateService))]
public class ChatReadStateServiceTest
{
    private readonly Mock<IChatRepository> _repo = new();
    private readonly ChatReadStateService _service;

    public ChatReadStateServiceTest()
    {
        _service = new ChatReadStateService(_repo.Object);
    }

    [Fact]
    public async Task MarkReadAsync_WhenUserIsNotMember_ThrowsInvalidOperationException()
    {
        _repo
            .Setup(x => x.GetMemberAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConversationMember?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.MarkReadAsync(1, 10, 5, CancellationToken.None));

        Assert.Equal("not_member", ex.Message);

        _repo.Verify(x => x.UpdateLastReadAsync(
            It.IsAny<ConversationMember>(),
            It.IsAny<int>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkReadAsync_WhenLastReadIsNull_UpdatesLastReadAndReturnsUnreadCount()
    {
        var member = new ConversationMember
        {
            UserId = 10,
            ChatConversationId = 1,
            LastReadMessageId = null
        };

        _repo
            .Setup(x => x.GetMemberAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        _repo
            .Setup(x => x.GetUnreadCountForUserAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await _service.MarkReadAsync(1, 10, 5, CancellationToken.None);

        Assert.Equal(3, result);

        _repo.Verify(x => x.UpdateLastReadAsync(
            member,
            1,
            5,
            It.IsAny<CancellationToken>()), Times.Once);

        _repo.Verify(x => x.GetUnreadCountForUserAsync(
            1,
            10,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkReadAsync_WhenNewLastReadIsGreaterThanCurrent_UpdatesLastRead()
    {
        var member = new ConversationMember
        {
            UserId = 10,
            ChatConversationId = 1,
            LastReadMessageId = 4
        };

        _repo
            .Setup(x => x.GetMemberAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        _repo
            .Setup(x => x.GetUnreadCountForUserAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _service.MarkReadAsync(1, 10, 5, CancellationToken.None);

        Assert.Equal(0, result);

        _repo.Verify(x => x.UpdateLastReadAsync(
            member,
            1,
            5,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkReadAsync_WhenNewLastReadIsEqualToCurrent_DoesNotUpdateLastRead()
    {
        var member = new ConversationMember
        {
            UserId = 10,
            ChatConversationId = 1,
            LastReadMessageId = 5
        };

        _repo
            .Setup(x => x.GetMemberAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        _repo
            .Setup(x => x.GetUnreadCountForUserAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _service.MarkReadAsync(1, 10, 5, CancellationToken.None);

        Assert.Equal(2, result);

        _repo.Verify(x => x.UpdateLastReadAsync(
            It.IsAny<ConversationMember>(),
            It.IsAny<int>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkReadAsync_WhenNewLastReadIsLowerThanCurrent_DoesNotUpdateLastRead()
    {
        var member = new ConversationMember
        {
            UserId = 10,
            ChatConversationId = 1,
            LastReadMessageId = 10
        };

        _repo
            .Setup(x => x.GetMemberAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        _repo
            .Setup(x => x.GetUnreadCountForUserAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _service.MarkReadAsync(1, 10, 5, CancellationToken.None);

        Assert.Equal(1, result);

        _repo.Verify(x => x.UpdateLastReadAsync(
            It.IsAny<ConversationMember>(),
            It.IsAny<int>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsRepositoryUnreadCount()
    {
        _repo
            .Setup(x => x.GetUnreadCountForUserAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var result = await _service.GetUnreadCountAsync(1, 10, CancellationToken.None);

        Assert.Equal(7, result);
    }
}