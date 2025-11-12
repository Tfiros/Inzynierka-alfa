using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("item")]
public partial class Item
{
    [Column("id")]
    public int ID { get; set; }
    [Column("game_id")]
    public int Game_ID { get; set; }
    [Column("name")]
    public string Name { get; set; } = null!;
    [Column("photo_url")]
    public string Photo_URL { get; set; } = null!;
    
    public virtual Game Game { get; set; } = null!;

    public virtual ICollection<ListingCounterOfferItem> ListingCounterOfferItems { get; set; } = new List<ListingCounterOfferItem>();

    public virtual ICollection<listing_item> ListingItems { get; set; } = new List<listing_item>();
}
