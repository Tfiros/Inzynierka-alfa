using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;

[Table("conversation_member")]
public sealed class ConversationMember
{
    [Column("user_id")]
    public int UserId { get; set; }
    [Column("chat_conversation_id")]
    public int ChatConversationId { get; set; }
    [Column("last_read_message_id")]
    public long? LastReadMessageId { get; set; }
    [Column("last_read_message_chat_conversation_id")]
    public int? LastReadMessageChatConversationId { get; set; }

    public User User { get; set; } = null!;
    public ChatConversation ChatConversation { get; set; } = null!;

    public ChatMessage? LastReadMessage { get; set; }
}