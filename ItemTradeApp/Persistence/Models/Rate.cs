using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;

[Table("Rate")]
public partial class Rate
{
    [Column("user_id")]
    public int UserId { get; set; }

    [Column("trade_id")]
    public int TradeId { get; set; }

    [Column("mark", TypeName = "decimal(3,1)")]
    public decimal Mark { get; set; }

    [Column("description")]
    [StringLength(500)]
    public string? Description { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual Trade Trade { get; set; } = null!;
}
