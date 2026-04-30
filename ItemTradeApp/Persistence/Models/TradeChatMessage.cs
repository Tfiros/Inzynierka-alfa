using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;

[Table("trade_chat_message")]
public partial class TradeChatMessage
{
    [Column("id")]
    public int Id { get; set; }
    
    [Column("trade_chat_id")]
    public int TradeChatId { get; set; }
    
    [Column("sender_id")]
    public int SenderId { get; set; }

    [Column("content")] 
    public string Content { get; set; } = null!;
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("edited_at")]
    public DateTime? EditedAt { get; set; }

    public virtual TradeChat TradeChat { get; set; } = null!;
    public virtual User Sender { get; set; } = null!;
    
}