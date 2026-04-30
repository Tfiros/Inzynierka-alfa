namespace ItemTradeApp.Features.TradeChats.DTOs.Response;

public sealed record TradeChatMessageDTO(
    int Id, int TradeChatId, int SenderId, string SenderNickname, 
    string Content, DateTime CreatedAtUtc, DateTime? EditedAtUtc
    );