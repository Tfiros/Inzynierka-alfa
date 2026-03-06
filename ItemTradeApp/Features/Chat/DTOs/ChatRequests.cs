namespace ItemTradeApp.Features.Chat.DTOs;
public sealed record SendMessageRequest(string Message);

public sealed record EditMessageRequest(string Message);

public sealed record MarkReadRequest(long LastReadMessageId);