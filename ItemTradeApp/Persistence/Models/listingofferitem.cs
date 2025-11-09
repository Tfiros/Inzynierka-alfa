using System;
using System.Collections.Generic;

namespace ItemTradeApp.Persistence.Models;

public partial class listingofferitem
{
    public int id { get; set; }

    public int item_id { get; set; }

    public int listingoffer_id { get; set; }

    public int quantity { get; set; }

    public virtual item item { get; set; } = null!;

    public virtual counteroffer listingoffer { get; set; } = null!;
}
