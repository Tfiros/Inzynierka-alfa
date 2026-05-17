using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("listing_counter_offer_items")]
public partial class ListingCounterOfferItem
{
    [Column("id")]
    public int ID { get; set; }
    [Column("item_id")]
    public int Item_ID { get; set; }
    [Column("counter_offer_id")]
    public int CounterOffers_ID { get; set; }
    [Column("quantity")]
    public int Quantity { get; set; }

    public virtual CounterOffer CounterOffer { get; set; } = null!;

    public virtual Item Item { get; set; } = null!;
}
