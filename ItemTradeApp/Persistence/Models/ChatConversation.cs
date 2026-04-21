using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("chat_conversation")]
public sealed class ChatConversation
{
    [Column("id")]  
    public int Id { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    [Column("name")]
    public string Name { get; set; } = null!;
    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    public ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}