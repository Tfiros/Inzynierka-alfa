using System;
using System.Collections.Generic;

namespace ItemTradeApp.Persistence.Models;

public partial class offer
{
    public int id { get; set; }

    public int user_id { get; set; }

    public DateTime expdate { get; set; }

    public int tokencost { get; set; }

    public virtual ICollection<counteroffer> counteroffers { get; set; } = new List<counteroffer>();

    public virtual ICollection<listingitem> listingitems { get; set; } = new List<listingitem>();

    public virtual ICollection<trade> trades { get; set; } = new List<trade>();

    public virtual User user { get; set; } = null!;
}
