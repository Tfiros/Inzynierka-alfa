using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ItemTradeApp.Persistence.Models;
[Table("genre")]
public partial class Genre
{
    [Column("id")]
    public int ID { get; set; }
    [Column("name")]
    public string Name { get; set; } = null!;
    [Column("is_deleted")] 
    public bool IsDeteled { get; set; }
    public virtual ICollection<Game> Games { get; set; } = new List<Game>();
}
