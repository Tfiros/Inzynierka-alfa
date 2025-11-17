using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("trade_status")]
public partial class TradeStatus
{
    [Column("id")]
    public int ID { get; set; }
    [Column("status_name")]
    public string StatusName { get; set; } = null!;

    public virtual ICollection<Trade> Trades { get; set; } = new List<Trade>();
}
