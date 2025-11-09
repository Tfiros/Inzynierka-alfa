using System;
using System.Collections.Generic;

namespace ItemTradeApp.Persistence.Models;

public partial class counteroffer
{
    public int id { get; set; }

    public int user_id { get; set; }

    public DateTime creationdate { get; set; }

    public int tokensoffered { get; set; }

    public int offerstatus_id { get; set; }

    public int? offer_id { get; set; }

    public virtual ICollection<listingofferitem> listingofferitems { get; set; } = new List<listingofferitem>();

    public virtual offer? offer { get; set; }

    public virtual offerstatus offerstatus { get; set; } = null!;

    public virtual User user { get; set; } = null!;
}
