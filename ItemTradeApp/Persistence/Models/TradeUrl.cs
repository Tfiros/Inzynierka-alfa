using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;

[Table("trade_urls")]
public sealed class TradeUrl
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("trade_id")]
    public int TradeId { get; set; }
    [Column("is_buyers")]
    public bool IsBuyers { get; set; }
    [Column("photo_url")]
    [MaxLength(2048)]
   
    public string PhotoUrl { get; set; } = string.Empty;

    public Trade Trade { get; set; } = null!;
}