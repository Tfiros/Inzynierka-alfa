using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;

[Table("trade_chat")]
public partial class TradeChat
{
    [Column("id")]
    public int Id { get; set; }
    
    [Column("trade_id")]
    public int TradeId { get; set; }
    
    [Column("participant_id")]
    public int ParticipantId { get; set; }
    
    [Column("middleman_id")]
    public int MiddlemanId { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }
    
    [Column("participant_last_read_message_id")]
    public int? ParticipantLastReadMessageId { get; set; }
    
    [Column("middleman_last_read_message_id")]
    public int? MiddlemanLastReadMessageId { get; set; }

    public virtual Trade Trade { get; set; } = null!;
    
    public virtual User Participant { get; set; } = null!;
    
    public virtual User Middleman { get; set; } = null!;
    
    public virtual ICollection<TradeChatMessage> Messages { get; set; } = new List<TradeChatMessage>();

}