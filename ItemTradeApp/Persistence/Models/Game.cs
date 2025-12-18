using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("game")]
public partial class Game
{
    [Column("id")]
    public int ID { get; set; }
    [Column("name")]
    public string Name { get; set; } = null!;
    [Column("photo_url")]
    public string Photo_URL { get; set; } = null!;
    [Column("genre_id")]
    public int Genre_ID { get; set; }
    [Column("is_deleted")] 
    public bool IsDeteled { get; set; }
    public virtual Genre Genre { get; set; } = null!;

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();
}
