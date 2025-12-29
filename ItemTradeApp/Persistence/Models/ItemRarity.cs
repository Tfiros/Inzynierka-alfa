using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("item_rarity")]
public class ItemRarity
{
    [Column("id")]
    public int ID { get; set; }
    [Column("game_id")]
    public int GameId { get; set; }
    [Column("rarity_name")]
    public string RarityName { get; set; } = null!;
    [Column("is_deleted")] 
    public bool IsDeleted { get; set; }
    public Game Game { get; set; } = null!;
    public ICollection<Item> Items { get; set; } = new List<Item>();
}
