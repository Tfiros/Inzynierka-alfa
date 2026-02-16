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
    public DateOnly ExpDate { get; set; }
    [Column("creation_date")]
    public DateTime CreationDate { get; set; }
    [Column("token_cost")]
    public int TokenCost { get; set; }
    [Column("offer_status_id")]
    public int OfferStatus_ID { get; set; }
    [Column("title")]
    public string title { get; set; }
    [Column("description")]
    public string description { get; set; }


    [Column("title")] 
    public string Title { get; set; } = null!;
    [Column("description")] 
    public string Description { get; set; } = null!;
    [Column("is_highlighted")]
    public bool IsHighlighted { get; set; }

    public virtual ICollection<CounterOffer> CounterOffers { get; set; } = new List<CounterOffer>();

    public virtual ICollection<ListingItems> ListingItems { get; set; } = new List<ListingItems>();

    public virtual OfferStatus OfferStatus { get; set; } = null!;

    public virtual ICollection<Trade> Trades { get; set; } = new List<Trade>();

    public virtual User User { get; set; } = null!;
}
