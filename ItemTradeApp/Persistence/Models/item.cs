using System;
using System.Collections.Generic;

namespace ItemTradeApp.Persistence.Models;

public partial class item
{
    public int id { get; set; }

    public int game_id { get; set; }

    public string name { get; set; } = null!;

    public string photourl { get; set; } = null!;

    public virtual game game { get; set; } = null!;

    public virtual ICollection<listingitem> listingitems { get; set; } = new List<listingitem>();

    public virtual ICollection<listingofferitem> listingofferitems { get; set; } = new List<listingofferitem>();
}
