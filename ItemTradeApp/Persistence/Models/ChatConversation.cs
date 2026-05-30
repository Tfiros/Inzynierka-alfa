using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("chat_conversation")]
public sealed class ChatConversation
{
    [Column("id")]  
    public int Id { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("trade_id")] 
    public int TradeId { get; set; }
    [Column("closed_at")] 
    public DateTime? ClosedAt { get; set; }


    public Trade Trade { get; set; } = null!;
    public ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
