using System;
using System.Collections.Generic;

namespace ItemTradeApp.Persistence.Models;

public partial class game
{
    public int id { get; set; }

    public string name { get; set; } = null!;

    public string photourl { get; set; } = null!;

    public int genre_id { get; set; }

    public virtual genre genre { get; set; } = null!;

    public virtual ICollection<item> items { get; set; } = new List<item>();
}
