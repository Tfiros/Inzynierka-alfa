using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("listing_item")]
public partial class listing_item
{
    [Column("id")]
    public int ID { get; set; }
    [Column("offer_id")]
    public int Offer_ID { get; set; }
    [Column("item_id")]
    public int Item_ID { get; set; }
    [Column("quantity")]
    public int Quantity { get; set; }

    public virtual Item Item { get; set; } = null!;

    public virtual Offer Offer { get; set; } = null!;
}
