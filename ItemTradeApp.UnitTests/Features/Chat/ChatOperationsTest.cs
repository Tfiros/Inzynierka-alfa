using ItemTradeApp.Features.Chat;
using ItemTradeApp.Features.Chat.Helpers;
using ItemTradeApp.Features.Chat.Repositories;
using ItemTradeApp.Features.Shared.Chat;
using ItemTradeApp.Persistence.Models;
using JetBrains.Annotations;
using Moq;

namespace ItemTradeApp.UnitTests.Features.Chat;

[TestSubject(typeof(ChatOperations))]
public class ChatOperationsTest
{
    private readonly Mock<IChatRepository> _repo = new();
    private readonly Mock<IChatRealtimePublisher> _publisher = new();
    private readonly FakeTimeProvider _timeProvider;
    private readonly ChatOperations _operations;

    public ChatOperationsTest()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 10, 12, 30, 0, TimeSpan.Zero));

        _operations = new ChatOperations(
            _repo.Object,
            _publisher.Object,
            _timeProvider);
    }

    [Fact]
    public async Task CreateChatsForTradeAsync_CreatesBuyerMiddlemanAndSellerMiddlemanChats()
    {
        var ctx = new CreateChatsForTradeContext(
            TradeId: 100,
            BuyerId: 1,
            SellerId: 2,
            MiddlemanId: 3);

        ChatConversation[]? capturedChats = null;

        _repo
            .Setup(x => x.AddChatsAsync(It.IsAny<IEnumerable<ChatConversation>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatConversation>, CancellationToken>((chats, _) =>
            {
                capturedChats = chats.ToArray();
            })
            .Returns(Task.CompletedTask);

        await _operations.CreateChatsForTradeAsync(ctx, CancellationToken.None);

        Assert.NotNull(capturedChats);
        Assert.Equal(2, capturedChats!.Length);

        var buyerChat = capturedChats[0];
        var sellerChat = capturedChats[1];

        Assert.Equal(100, buyerChat.TradeId);
        Assert.False(buyerChat.IsDeleted);
        Assert.Null(buyerChat.ClosedAt);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, buyerChat.CreatedAt);
        Assert.Contains(buyerChat.Members, x => x.UserId == 1);
        Assert.Contains(buyerChat.Members, x => x.UserId == 3);

        Assert.Equal(100, sellerChat.TradeId);
        Assert.False(sellerChat.IsDeleted);
        Assert.Null(sellerChat.ClosedAt);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, sellerChat.CreatedAt);
        Assert.Contains(sellerChat.Members, x => x.UserId == 2);
        Assert.Contains(sellerChat.Members, x => x.UserId == 3);

        _repo.Verify(x => x.AddChatsAsync(
            It.IsAny<IEnumerable<ChatConversation>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloseChatsForTradeAsync_CallsRepositoryWithCurrentUtcTime()
    {
        await _operations.CloseChatsForTradeAsync(100, CancellationToken.None);

        _repo.Verify(x => x.CloseChatsForTradeAsync(
            100,
            _timeProvider.GetUtcNow().UtcDateTime,
            It.IsAny<CancellationToken>()), Times.Once);
    }

   [Fact]
public async Task PublishChatsClosedAsync_PublishesEveryClosedChat()
{
    var closedAt = new DateTime(2026, 1, 10, 12, 30, 0, DateTimeKind.Utc);

    IReadOnlyList<(int Id, DateTime ClosedAtUtc, string[] MemberAuth0Ids)> chats =
        new List<(int Id, DateTime ClosedAtUtc, string[] MemberAuth0Ids)>
        {
            (1, closedAt, ["auth0-a", "auth0-b"]),
            (2, closedAt, ["auth0-c", "auth0-d"])
        };

    _repo
        .Setup(x => x.GetClosedChatsForPublish(100, It.IsAny<CancellationToken>()))
        .ReturnsAsync(chats);

    await _operations.PublishChatsClosedAsync(100, CancellationToken.None);

    _publisher.Verify(x => x.PublishChatClosedAsync(
        1,
        closedAt,
        It.Is<IReadOnlyCollection<string>>(m => m.Contains("auth0-a") && m.Contains("auth0-b")),
        It.IsAny<CancellationToken>()), Times.Once);

    _publisher.Verify(x => x.PublishChatClosedAsync(
        2,
        closedAt,
        It.Is<IReadOnlyCollection<string>>(m => m.Contains("auth0-c") && m.Contains("auth0-d")),
        It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task PublishChatsCreatedAsync_PublishesEveryCreatedChat()
{
    IReadOnlyList<(int Id, int TradeResponseId, string[] MemberAuth0Ids)> chats =
        new List<(int Id, int TradeResponseId, string[] MemberAuth0Ids)>
        {
            (1, 100, ["auth0-a", "auth0-b"]),
            (2, 100, ["auth0-c", "auth0-d"])
        };

    _repo
        .Setup(x => x.GetCreatedChatsForPublish(100, It.IsAny<CancellationToken>()))
        .ReturnsAsync(chats);

    await _operations.PublishChatsCreatedAsync(100, CancellationToken.None);

    _publisher.Verify(x => x.PublishChatCreatedAsync(
        1,
        100,
        It.Is<IReadOnlyCollection<string>>(m => m.Contains("auth0-a") && m.Contains("auth0-b")),
        It.IsAny<CancellationToken>()), Times.Once);

    _publisher.Verify(x => x.PublishChatCreatedAsync(
        2,
        100,
        It.Is<IReadOnlyCollection<string>>(m => m.Contains("auth0-c") && m.Contains("auth0-d")),
        It.IsAny<CancellationToken>()), Times.Once);
}
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