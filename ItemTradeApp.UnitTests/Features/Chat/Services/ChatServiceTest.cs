using ItemTradeApp.Features.Chat.DTOs;
using ItemTradeApp.Features.Chat.Helpers;
using ItemTradeApp.Features.Chat.Repositories;
using ItemTradeApp.Features.Chat.Services;
using ItemTradeApp.Persistence.Models;
using JetBrains.Annotations;
using Moq;

namespace ItemTradeApp.UnitTests.Features.Chat.Services;

[TestSubject(typeof(ChatService))]
public class ChatServiceTest
{
    private readonly Mock<IChatRepository> _repo = new();
    private readonly Mock<IChatThreadsReader> _threadsReader = new();
    private readonly Mock<IChatReadStateService> _readStateService = new();
    private readonly Mock<IChatRealtimePublisher> _publisher = new();
    private readonly Mock<IChatUserResolver> _userResolver = new();
    private readonly FakeTimeProvider _timeProvider;

    private readonly ChatService _service;

    public ChatServiceTest()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 10, 12, 30, 0, TimeSpan.Zero));

        _service = new ChatService(
            _repo.Object,
            _threadsReader.Object,
            _readStateService.Object,
            _publisher.Object,
            _userResolver.Object,
            _timeProvider);
    }

    [Fact]
    public async Task IsMemberAsync_ReturnsRepositoryResult()
    {
        _repo
            .Setup(x => x.IsMemberAsync(1, "abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.IsMemberAsync(1, "abc", CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task AddMessageAsync_WhenChatIdInvalid_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddMessageAsync(0, "auth0|abc", "hello", CancellationToken.None));

        Assert.Equal("invalid_chat_id", ex.Message);
    }

    [Fact]
    public async Task AddMessageAsync_WhenMessageEmpty_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddMessageAsync(1, "auth0|abc", "   ", CancellationToken.None));

        Assert.Equal("message_empty", ex.Message);
    }

    [Fact]
    public async Task AddMessageAsync_WhenAuth0IsInvalid_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AddMessageAsync(1, "", "hello", CancellationToken.None));

        Assert.Equal("sender_not_found", ex.Message);
    }

    [Fact]
    public async Task AddMessageAsync_WhenSenderNotFound_ThrowsInvalidOperationException()
    {
        _repo
            .Setup(x => x.GetUserIdByAuth0Async("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AddMessageAsync(1, "auth0|abc", "hello", CancellationToken.None));

        Assert.Equal("sender_not_found", ex.Message);
    }

    [Fact]
    public async Task AddMessageAsync_WhenSenderIsNotMember_ThrowsInvalidOperationException()
    {
        _repo
            .Setup(x => x.GetUserIdByAuth0Async("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        _repo
            .Setup(x => x.IsMemberAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AddMessageAsync(1, "auth0|abc", "hello", CancellationToken.None));

        Assert.Equal("not_member", ex.Message);
    }

    [Fact]
    public async Task AddMessageAsync_WhenChatDoesNotExist_ThrowsKeyNotFoundException()
    {
        _repo
            .Setup(x => x.GetUserIdByAuth0Async("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        _repo
            .Setup(x => x.IsMemberAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repo
            .Setup(x => x.ChatExistsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.AddMessageAsync(1, "auth0|abc", "hello", CancellationToken.None));

        Assert.Equal("chat_not_found", ex.Message);
    }

    [Fact]
    public async Task AddMessageAsync_WhenChatIsClosed_ThrowsInvalidOperationException()
    {
        _repo
            .Setup(x => x.GetUserIdByAuth0Async("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        _repo
            .Setup(x => x.IsMemberAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repo
            .Setup(x => x.ChatExistsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repo
            .Setup(x => x.IsChatClosedAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AddMessageAsync(1, "auth0|abc", "hello", CancellationToken.None));

        Assert.Equal("chat_closed", ex.Message);
    }

    [Fact]
    public async Task AddMessageAsync_WhenValid_AddsMessageAndPublishesThreadUpdated()
    {
        var createdAt = _timeProvider.GetUtcNow().UtcDateTime;

        var dto = new ChatMessageDto(
            Id: 100,
            ChatConversationId: 1,
            SenderId: 10,
            Message: "hello",
            CreatedAt: createdAt,
            EditedAt: null);

        var messageEntity = new ChatMessage
        {
            Id = 100,
            ChatConversationId = 1,
            SenderId = 10,
            Message = "hello",
            CreatedAt = createdAt
        };

        _repo
            .Setup(x => x.GetUserIdByAuth0Async("abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        _repo
            .Setup(x => x.IsMemberAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repo
            .Setup(x => x.ChatExistsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repo
            .Setup(x => x.IsChatClosedAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repo
            .Setup(x => x.AddMessageAsync(1, 10, "hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        _repo
            .Setup(x => x.GetMessageByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageEntity);

        var result = await _service.AddMessageAsync(1, "auth0|abc", "  hello  ", CancellationToken.None);

        Assert.Equal(100, result.Id);
        Assert.Equal("hello", result.Message);

        _repo.Verify(x => x.AddMessageAsync(
            1,
            10,
            "hello",
            It.IsAny<CancellationToken>()), Times.Once);

        _publisher.Verify(x => x.PublishThreadUpdatedToMembersAsync(
            1,
            messageEntity,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMessagesAsync_WhenChatIdInvalid_ReturnsBadRequest()
    {
        var result = await _service.GetMessagesAsync(0, null, 20, "auth0|abc", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("chatId must be > 0.", result.Message);
    }

    [Fact]
    public async Task GetMessagesAsync_WhenUserResolverFails_ReturnsUnauthorized()
    {
        _userResolver
            .Setup(x => x.TryGetUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, "User not found"));

        var result = await _service.GetMessagesAsync(1, null, 20, "auth0|abc", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("User not found", result.Message);
    }

    [Fact]
    public async Task GetMessagesAsync_WhenUserIsNotMember_ReturnsForbidden()
    {
        var user = ExistingUser();

        _userResolver
            .Setup(x => x.TryGetUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((user, null));

        _repo
            .Setup(x => x.IsMemberAsync(1, user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.GetMessagesAsync(1, null, 20, "auth0|abc", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("not_member", result.Message);
    }

    [Fact]
    public async Task GetMessagesAsync_WhenUserIsMember_ReturnsMessages()
    {
        var user = ExistingUser();

        var messages = new List<ChatMessageDto>
        {
            new (
                Id: 1,
                ChatConversationId: 1,
                SenderId: user.ID,
                Message: "hello",
                CreatedAt: _timeProvider.GetUtcNow().UtcDateTime,
                EditedAt: null)
        };

        _userResolver
            .Setup(x => x.TryGetUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((user, null));

        _repo
            .Setup(x => x.IsMemberAsync(1, user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repo
            .Setup(x => x.GetMessagesAsync(1, null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        var result = await _service.GetMessagesAsync(1, null, 20, "auth0|abc", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!);
        Assert.Equal("Successfully retrieved.", result.Message);
    }

    [Fact]
    public async Task EditMessageAsync_WhenMessageIdInvalid_ReturnsBadRequest()
    {
        var result = await _service.EditMessageAsync(
            0,
            new EditMessageRequest("hello"),
            "auth0|abc",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("messageId must be > 0.", result.Message);
    }

    [Fact]
    public async Task EditMessageAsync_WhenRequestIsNull_ReturnsBadRequest()
    {
        var result = await _service.EditMessageAsync(
            1,
            null,
            "auth0|abc",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Body is required.", result.Message);
    }

    [Fact]
    public async Task EditMessageAsync_WhenMessageIsEmpty_ReturnsBadRequest()
    {
        var result = await _service.EditMessageAsync(
            1,
            new EditMessageRequest("   "),
            "auth0|abc",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("message_required", result.Message);
    }

    [Fact]
    public async Task EditMessageAsync_WhenMessageNotFound_ReturnsNotFound()
    {
        var user = ExistingUser();

        _userResolver
            .Setup(x => x.TryGetUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((user, null));

        _repo
            .Setup(x => x.GetMessageForEditAsync(1, user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatMessage?)null);

        var result = await _service.EditMessageAsync(
            1,
            new EditMessageRequest("updated"),
            "auth0|abc",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("message_not_found", result.Message);
    }

    [Fact]
    public async Task EditMessageAsync_WhenChatIsClosed_ReturnsForbidden()
    {
        var user = ExistingUser();
        var message = ExistingMessage(user.ID);

        _userResolver
            .Setup(x => x.TryGetUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((user, null));

        _repo
            .Setup(x => x.GetMessageForEditAsync(message.Id, user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _repo
            .Setup(x => x.IsChatClosedAsync(message.ChatConversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.EditMessageAsync(
            message.Id,
            new EditMessageRequest("updated"),
            "auth0|abc",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("chat_closed", result.Message);
    }

    [Fact]
    public async Task EditMessageAsync_WhenEditWindowExpired_ReturnsForbidden()
    {
        var user = ExistingUser();
        var message = ExistingMessage(user.ID);
        message.CreatedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-6);

        _userResolver
            .Setup(x => x.TryGetUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((user, null));

        _repo
            .Setup(x => x.GetMessageForEditAsync(message.Id, user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _repo
            .Setup(x => x.IsChatClosedAsync(message.ChatConversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.EditMessageAsync(
            message.Id,
            new EditMessageRequest("updated"),
            "auth0|abc",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("edit_window_expired", result.Message);
    }

    [Fact]
    public async Task EditMessageAsync_WhenValid_UpdatesMessageSavesAndPublishesEvents()
    {
        var user = ExistingUser();
        var message = ExistingMessage(user.ID);
        message.CreatedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-2);

        _userResolver
            .Setup(x => x.TryGetUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((user, null));

        _repo
            .Setup(x => x.GetMessageForEditAsync(message.Id, user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _repo
            .Setup(x => x.IsChatClosedAsync(message.ChatConversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.EditMessageAsync(
            message.Id,
            new EditMessageRequest("  updated message  "),
            "auth0|abc",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("updated message", message.Message);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, message.EditedAt);
        Assert.Equal("updated message", result.Data!.Message);

        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _publisher.Verify(x => x.PublishMessageUpdatedAsync(
            message,
            It.IsAny<CancellationToken>()), Times.Once);

        _publisher.Verify(x => x.PublishThreadUpdatedToMembersAsync(
            message.ChatConversationId,
            message,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteMessageAsync_WhenMessageIdInvalid_ReturnsBadRequest()
    {
        var result = await _service.DeleteMessageAsync(0, "auth0|abc", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("messageId must be > 0.", result.Message);
    }

    [Fact]
    public async Task DeleteMessageAsync_WhenMessageNotFound_ReturnsNotFound()
    {
        var user = ExistingUser();

        _userResolver
            .Setup(x => x.TryGetUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((user, null));

        _repo
            .Setup(x => x.GetMessageForEditAsync(1, user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatMessage?)null);

        var result = await _service.DeleteMessageAsync(1, "auth0|abc", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("message_not_found", result.Message);
    }

    [Fact]
    public async Task DeleteMessageAsync_WhenChatIsClosed_ReturnsForbidden()
    {
        var user = ExistingUser();
        var message = ExistingMessage(user.ID);

        _userResolver
            .Setup(x => x.TryGetUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((user, null));

        _repo
            .Setup(x => x.GetMessageForEditAsync(message.Id, user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _repo
            .Setup(x => x.IsChatClosedAsync(message.ChatConversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.DeleteMessageAsync(message.Id, "auth0|abc", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("chat_closed", result.Message);
    }

    [Fact]
    public async Task DeleteMessageAsync_WhenValid_SoftDeletesAndPublishesEvents()
    {
        var user = ExistingUser();
        var message = ExistingMessage(user.ID);

        _userResolver
            .Setup(x => x.TryGetUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((user, null));

        _repo
            .Setup(x => x.GetMessageForEditAsync(message.Id, user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);

        _repo
            .Setup(x => x.IsChatClosedAsync(message.ChatConversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.DeleteMessageAsync(message.Id, "auth0|abc", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Message deleted.", result.Data);
        _repo.Verify(x => x.SoftDeleteMessageAsync(
            message,
            It.IsAny<CancellationToken>()), Times.Once);

        _publisher.Verify(x => x.PublishMessageDeletedAsync(
            message.Id,
            message.ChatConversationId,
            It.IsAny<CancellationToken>()), Times.Once);

        _publisher.Verify(x => x.PublishThreadUpdatedToMembersAsync(
            message.ChatConversationId,
            message,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkReadAsync_WhenChatIdInvalid_ReturnsBadRequest()
    {
        var result = await _service.MarkReadAsync(
            0,
            new MarkReadRequest(1),
            "auth0|abc",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("chatId must be > 0.", result.Message);
    }

    [Fact]
    public async Task MarkReadAsync_WhenRequestIsNull_ReturnsBadRequest()
    {
        var result = await _service.MarkReadAsync(
            1,
            null,
            "auth0|abc",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Body is required.", result.Message);
    }

    [Fact]
    public async Task MarkReadAsync_WhenLastReadMessageIdInvalid_ReturnsBadRequest()
    {
        var result = await _service.MarkReadAsync(
            1,
            new MarkReadRequest(0),
            "auth0|abc",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("lastReadMessageId must be > 0.", result.Message);
    }

    [Fact]
    public async Task MarkReadAsync_WhenUserIsNotMember_ReturnsForbidden()
    {
        var user = ExistingUser();

        _userResolver
            .Setup(x => x.TryGetUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((user, null));

        _repo
            .Setup(x => x.IsMemberAsync(1, user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.MarkReadAsync(
            1,
            new MarkReadRequest(5),
            "auth0|abc",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("not_member", result.Message);
    }

    [Fact]
    public async Task MarkReadAsync_WhenValid_CallsReadStateServiceAndPublishesThreadRead()
    {
        var user = ExistingUser();

        _userResolver
            .Setup(x => x.TryGetUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((user, null));

        _repo
            .Setup(x => x.IsMemberAsync(1, user.ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _readStateService
            .Setup(x => x.MarkReadAsync(1, user.ID, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _service.MarkReadAsync(
            1,
            new MarkReadRequest(5),
            "auth0|abc",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data!.ChatConversationId);
        Assert.Equal(5, result.Data.LastReadMessageId);
        Assert.Equal(0, result.Data.UnreadCount);

        _publisher.Verify(x => x.PublishThreadReadAsync(
            "abc",
            1,
            5,
            0,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetThreadsAsync_WhenUserResolverFails_ReturnsUnauthorized()
    {
        _userResolver
            .Setup(x => x.TryGetUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, "User not found"));

        var result = await _service.GetThreadsAsync(1, 20, null, "auth0|abc", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("User not found", result.Message);
    }

    [Fact]
    public async Task GetThreadsAsync_WhenUserExists_ReturnsThreads()
    {
        var user = ExistingUser();

        var threads = new List<ChatThreadListItemDto>();

        _userResolver
            .Setup(x => x.TryGetUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((user, null));

        _threadsReader
            .Setup(x => x.GetThreadsAsync(user.ID, 1, 20, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(threads);

        var result = await _service.GetThreadsAsync(1, 20, null, "auth0|abc", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("Successfully retrieved.", result.Message);
    }

    [Fact]
    public async Task GetChatsForTradeAsync_WhenUserResolverFails_ReturnsUnauthorized()
    {
        _userResolver
            .Setup(x => x.TryGetUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, "User not found"));

        var result = await _service.GetChatsForTradeAsync(100, "auth0|abc", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("User not found", result.Message);
    }

    [Fact]
    public async Task GetChatsForTradeAsync_WhenUserExists_ReturnsChats()
    {
        var user = ExistingUser();

        var rows = new List<ChatThreadListItemDto>();

        _userResolver
            .Setup(x => x.TryGetUserAsync("auth0|abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync((user, null));

        _threadsReader
            .Setup(x => x.GetChatsForTradeAsync(user.ID, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var result = await _service.GetChatsForTradeAsync(100, "auth0|abc", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("Successfully retrieved", result.Message);
    }

    private static User ExistingUser()
        => new()
        {
            ID = 10,
            Auth0UserID = "abc"
        };

    private ChatMessage ExistingMessage(int senderId)
        => new()
        {
            Id = 1,
            ChatConversationId = 100,
            SenderId = senderId,
            Message = "hello",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-1),
            EditedAt = null,
            DeletedAt = null
        };

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}