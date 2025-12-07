using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("trade")]
public partial class Trade
{
    [Column("id")]
    public int ID { get; set; }
    [Column("token_cost")]
    public int TokenCost { get; set; }
    [Column("creation_date")]
    public DateTime CreationDate { get; set; }
    [Column("completition_date")]
    public DateTime? CompletitionDate { get; set; }
    [Column("buyer_feedback")]
    public string? BuyerFeedback { get; set; }
    [Column("buyer_positivity")]
    public int? BuyerPositivity { get; set; }
    [Column("trade_status_id")]
    public int TradeStatus_ID { get; set; }
    [Column("offer_id")]
    public int Offer_ID { get; set; }
    [Column("customer_id")]
    public int Customer_ID { get; set; }
    [Column("middleman_user_id")]
    public int? MiddlemanUser_ID { get; set; }
    [Column("user_id")]
    public int User_ID { get; set; }

    public virtual User Customer { get; set; } = null!;

    public virtual User? MiddlemanUser { get; set; } = null!;

    public virtual Offer Offer { get; set; } = null!;

    public virtual TradeStatus TradeStatus { get; set; } = null!;
    // offer posting user
    public virtual User PostingUser { get; set; } = null!;
    public virtual ICollection<Rate> Rates { get; set; } = new List<Rate>();

}
