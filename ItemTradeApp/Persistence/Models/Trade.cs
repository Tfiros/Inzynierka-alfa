using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("trade")]
public partial class Trade
{
    [Column("id")]
    public int ID { get; set; }
    [Column("creation_date")]
    public DateTime CreationDate { get; set; }
    [Column("completition_date")]
    public DateTime? CompletitionDate { get; set; }
    [Column("trade_status_id")]
    public int TradeStatus_ID { get; set; }
    [Column("offer_id")]
    public int Offer_ID { get; set; }
    [Column("counter_offer_id")] 
    public int? AcceptedCounterOffer_ID { get; set; }
    [Column("customer_id")]
    public int Customer_ID { get; set; }
    [Column("middleman_user_id")]
    public int? MiddlemanUser_ID { get; set; }
    [Column("has_buyers_items")]
    public bool HasBuyersItems { get; set; } = false;

    [Column("has_sellers_items")]
    public bool HasSellersItems { get; set; } = false;
    public virtual User Customer { get; set; } = null!;

    public virtual User? MiddlemanUser { get; set; } = null!;

    public virtual Offer Offer { get; set; } = null!;
    public virtual CounterOffer AcceptedCounterOffer { get; set; } = null!;

    public virtual TradeStatus TradeStatus { get; set; } = null!;
    public virtual ICollection<Rate> Rates { get; set; } = new List<Rate>();
    public virtual List<TradeUrl> Urls { get; set; } = new();

}
