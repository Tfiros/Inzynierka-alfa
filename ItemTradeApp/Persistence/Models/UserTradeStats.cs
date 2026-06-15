using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;

[Table("user_trade_stats")]
public class UserTradeStats
{
    [Column("user_id")]
    public int UserId { get; set; }
    [Column("successful_trades")]
    public int SuccessfulTrades { get; set; }
    [Column("completed_trades")]
    public int CompletedTrades { get; set; }
    [Column("rating_sum")]
    public int RatingSum { get; set; }
    [Column("rating_count")]
    public int RatingCount { get; set; }

    public virtual User User { get; set; } = null!;
}