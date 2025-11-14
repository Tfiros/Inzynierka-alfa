using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("counter_offer")]
public partial class CounterOffer
{
    [Column("id")]
    public int ID { get; set; }
    [Column("user_id")]
    public int User_ID { get; set; }
    [Column("creation_date")]
    public DateTime CreationDate { get; set; }
    [Column("tokens_offered")]
    public int TokensOffered { get; set; }
    [Column("offer_status_id")]
    public int OfferStatus_Id { get; set; }
    [Column("offer_id")]
    public int Offer_Id { get; set; }

    public virtual ICollection<ListingCounterOfferItem> ListingCounterOfferItems { get; set; } = new List<ListingCounterOfferItem>();

    public virtual Offer Offer { get; set; } = null!;

    public virtual CounterOfferStatus OfferStatus { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
