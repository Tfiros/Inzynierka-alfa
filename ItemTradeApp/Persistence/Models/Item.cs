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
    [Column("is_deleted")] 
    public bool IsDeleted { get; set; }
    [Column("estimated_token_value")] 
    public int EstimatedTokenValue { get; set; }
    [Column("item_rarity_id")]
    public int ItemRarityId { get; set; }
    public ItemRarity ItemRarity { get; set; } = null!;
    public virtual Game Game { get; set; } = null!;
    public virtual ICollection<ListingCounterOfferItem> ListingCounterOfferItems { get; set; } = new List<ListingCounterOfferItem>();

    public virtual ICollection<ListingItems> ListingItems { get; set; } = new List<ListingItems>();
}
