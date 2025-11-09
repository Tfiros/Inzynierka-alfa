using System;
using System.Collections.Generic;

namespace ItemTradeApp.Persistence.Models;

public partial class listingitem
{
    public int id { get; set; }

    public int offer_id { get; set; }

    public int item_id { get; set; }

    public int quantity { get; set; }

    public virtual item item { get; set; } = null!;

    public virtual offer offer { get; set; } = null!;
}
