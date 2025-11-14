using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("counter_offer_status")]
public partial class CounterOfferStatus
{
    [Column("id")]
    public int ID { get; set; }
    [Column("status_name")]
    public string StatusName { get; set; } = null!;

    public virtual ICollection<CounterOffer> CounterOffers { get; set; } = new List<CounterOffer>();
}
