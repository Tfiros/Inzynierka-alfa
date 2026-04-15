using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;

[Table("chat_messages")]
public sealed class ChatMessage
{
    [Column("id")]
    public long Id { get; set; }
    [Column("chat_conversation_id")]
    public int ChatConversationId { get; set; }
    [Column("sender_id")]
    public int SenderId { get; set; }
    [Column("message")]
    public string Message { get; set; } = null!;
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    [Column("edited_at")]
    public DateTime? EditedAt { get; set; }
    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    public ChatConversation ChatConversation { get; set; } = null!;
    public User Sender { get; set; } = null!;
}