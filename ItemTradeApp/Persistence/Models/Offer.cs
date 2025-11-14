using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("offer")]
public partial class Offer
{
    [Column("id")]
    public int ID { get; set; }
    [Column("user_id")]
    public int User_ID { get; set; }
    [Column("exp_date")]
    public DateTime ExpDate { get; set; }
    [Column("token_cost")]
    public int TokenCost { get; set; }
    [Column("offer_status_id")]
    public int OfferStatus_ID { get; set; }

    public virtual ICollection<CounterOffer> CounterOffers { get; set; } = new List<CounterOffer>();

    public virtual ICollection<listing_item> ListingItems { get; set; } = new List<listing_item>();

    public virtual offer_status OfferStatus { get; set; } = null!;

    public virtual ICollection<Trade> Trades { get; set; } = new List<Trade>();

    public virtual User User { get; set; } = null!;
}
