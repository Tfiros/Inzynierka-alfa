using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;

[Table("Rate")]
public partial class Rate
{
    [Column("User_Id")]
    public int UserId { get; set; }

    [Column("Trade_Id")]
    public int TradeId { get; set; }

    [Column("Mark", TypeName = "decimal(3,1)")]
    public decimal Mark { get; set; }

    [Column("Description")]
    [StringLength(500)]
    public string? Description { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual Trade Trade { get; set; } = null!;
}
